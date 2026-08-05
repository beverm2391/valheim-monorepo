#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
installer="$root/scripts/install-server-mods.sh"
plugin_source="$root/server-mods/benheim-inventory/src/Plugin.cs"
plugin_project="$root/server-mods/benheim-inventory/src/BenheimInventory.csproj"
client_project="$root/mods/benheim-qol/src/BenheimQoL.csproj"
plugin="$root/server-mods/benheim-inventory/dist/BenheimInventory.dll"
verifier="$root/server/verify-benheim-inventory"
protocol="$root/shared/benheim-inventory-protocol"
tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

fail() {
  echo "FAIL: $1" >&2
  exit 1
}

assert_contains() {
  local message=$1
  local needle=$2
  local file=$3
  grep -Fq -- "$needle" "$file" || fail "$message"
}

bash -n "$installer"
bash -n "$root/server-mods/benheim-inventory/scripts/build.sh"
bash -n "$verifier"

dotnet run --project \
  "$root/tests/inventory-transaction-receipts/InventoryTransactionReceiptTests.csproj" \
  --configuration Release
dotnet run --project \
  "$root/tests/inventory-transaction-upgrade/InventoryTransactionUpgradeTests.csproj" \
  --configuration Release
dotnet run --project \
  "$root/tests/inventory-transaction-audit/InventoryTransactionAuditTests.csproj" \
  --configuration Release

expected_checksum=7cd9cb1461e8aa5794fef124a0f498826a78bbc380e45bc553d8ea758c9a286a
actual_checksum="$(shasum -a 256 "$plugin" | awk '{print $1}')"
[[ "$actual_checksum" == "$expected_checksum" ]] || fail "Benheim Inventory plugin checksum changed"
assert_contains "installer pins the inventory plugin checksum" "$expected_checksum" "$installer"
assert_contains "installer stages the inventory plugin" 'inventory_file=BenheimInventory.dll' "$installer"
assert_contains "installer deploys the inventory plugin" '/BepInEx/plugins/BenheimInventory/BenheimInventory.dll' "$installer"
assert_contains "installer verifies the inventory plugin" '"$work/verify-benheim-inventory" "$started_at"' "$installer"
assert_contains "failed rollout restores the exact prior state" 'recover_previous_state' "$installer"
assert_contains "rollback snapshots the existing BepInEx tree" '/opt/valheim/server/BepInEx' "$installer"
assert_contains "rollback snapshots the launcher" '/usr/local/bin/valheim-start' "$installer"
assert_contains "rollback snapshots the server environment" '/etc/valheim/server.env' "$installer"

assert_contains "server plugin pins version" 'PluginVersion = "0.1.2"' "$plugin_source"
assert_contains "server plugin logs exact protocol version" 'Benheim Inventory 0.1.2 loaded with protocol 2.' "$plugin_source"
assert_contains "server and client compile the same protocol source" 'shared/benheim-inventory-protocol/*.cs' "$plugin_project"
assert_contains "server and client compile the same protocol source" 'shared/benheim-inventory-protocol/*.cs' "$client_project"

if strings "$plugin" | grep -Fq "/Users/"; then
  fail "plugin binary contains a local user path"
fi
if strings "$plugin" | grep -Fiq "Jotunn"; then
  fail "plugin binary depends on Jotunn"
fi
if rg -F -g '*.cs' 'ClaimOwnership' "$protocol"; then
  fail "transaction protocol must not claim chest ownership"
fi

assert_contains "requests retry with the same immutable bytes" 'new ZPackage(pending.RequestBytes)' "$protocol/InventoryTransactionClient.cs"
assert_contains "server routes mutation to the validated owner" 'InvokeRoutedRPC(owner, OwnerExecuteRpc' "$protocol/InventoryTransactionServer.cs"
assert_contains "temporary owner loss leaves reservations pending" 'owner == 0L || !PeerHasProtocol(owner)' "$protocol/InventoryTransactionServer.cs"
assert_contains "partial application preserves observed acceptance" 'ApplyDeposit(container!, requestedItems, out fullyApplied)' "$protocol/InventoryTransactionOwner.cs"
assert_contains "owner rechecks ownership" '!view.IsOwner()' "$protocol/InventoryTransactionOwner.cs"
assert_contains "owner records duplicate-safe receipts" 'InventoryTransactionReceipts.Record' "$protocol/InventoryTransactionOwner.cs"
assert_contains "mismatched clients disable Put Away" 'connected.Count == compatible' "$protocol/InventoryTransactionCapabilities.cs"

cat > "$tmp_dir/journalctl" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" > "$MOCK_JOURNAL_ARGS"
cat "$MOCK_JOURNAL_LOG"
MOCK
chmod +x "$tmp_dir/journalctl"

printf '%s\n' 'Game server connected' > "$tmp_dir/journal.log"
if MOCK_JOURNAL_ARGS="$tmp_dir/journal.args" \
  MOCK_JOURNAL_LOG="$tmp_dir/journal.log" \
  JOURNALCTL_BIN="$tmp_dir/journalctl" \
  "$verifier" '2026-08-02T23:00:00-04:00' >/dev/null 2>&1; then
  fail "generic readiness must not satisfy the inventory load gate"
fi

printf '%s\n' 'Benheim Inventory 0.1.2 loaded with protocol 2.' > "$tmp_dir/journal.log"
MOCK_JOURNAL_ARGS="$tmp_dir/journal.args" \
MOCK_JOURNAL_LOG="$tmp_dir/journal.log" \
JOURNALCTL_BIN="$tmp_dir/journalctl" \
  "$verifier" '2026-08-02T23:00:00-04:00' >/dev/null
assert_contains "verification is bounded to current start" '--since 2026-08-02T23:00:00-04:00' "$tmp_dir/journal.args"

echo "PASS: Benheim Inventory protocol, binary, installer, and load gate"
