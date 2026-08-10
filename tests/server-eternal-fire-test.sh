#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
installer="$root/scripts/install-server-mods.sh"
source_file="$root/server-mods/benheim-eternal-fire/src/ZdoFuelPatches.cs"
supported_file="$root/server-mods/benheim-eternal-fire/src/SupportedFireplaces.cs"
plugin_source="$root/server-mods/benheim-eternal-fire/src/Plugin.cs"
plugin="$root/server-mods/benheim-eternal-fire/dist/BenheimEternalFire.dll"
verifier="$root/server/verify-benheim-eternal-fire"
recovery="$root/server/recover-valheim-vanilla"
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

assert_not_contains() {
  local message=$1
  local needle=$2
  local file=$3
  if grep -Fq -- "$needle" "$file"; then
    fail "$message"
  fi
}

bash -n "$installer"
bash -n "$root/server-mods/benheim-eternal-fire/scripts/build.sh"
bash -n "$verifier"
bash -n "$recovery"

dotnet run --project "$root/tests/refill-policy/RefillPolicyTests.csproj" \
  --configuration Release

expected_checksum=8f452cc68d839b7a843676c89b479e357c2b932db8f0f02106de5c5cfde451f4
actual_checksum="$(shasum -a 256 "$plugin" | awk '{print $1}')"
[[ "$actual_checksum" == "$expected_checksum" ]] || fail "first-party plugin checksum changed"
assert_contains "installer pins the first-party plugin checksum" "$expected_checksum" "$installer"
assert_contains "installer removes the old Jotunn directory" "/BepInEx/plugins/Jotunn" "$installer"
assert_contains "installer removes the old Eternal Fire directory" "/BepInEx/plugins/EternalFire" "$installer"
assert_contains "installer removes the obsolete Benheim Inventory directory" "/BepInEx/plugins/BenheimInventory" "$installer"
assert_contains "mod staging is restricted before transfer" 'install -d -m 0700 /tmp/valheim-server-mods' "$installer"
assert_contains "password-bearing rollback archive is root-only" 'chmod 0600 "$work/rollback/system.tar.gz.tmp"' "$installer"
assert_contains "mod staging is removed after recovery or success" 'rm -rf "$work"' "$installer"
assert_not_contains "installer must not download Jotunn" "ValheimModding-Jotunn" "$installer"
assert_not_contains "installer must not download upstream Eternal Fire" "Digitalroot-Eternal_Fire" "$installer"

if strings "$plugin" | grep -Fq "/Users/"; then
  fail "plugin binary contains a local user path"
fi
if strings "$plugin" | grep -Fiq "Jotunn"; then
  fail "plugin binary depends on Jotunn"
fi
assert_contains "plugin source pins version 0.1.1" 'PluginVersion = "0.1.1"' "$plugin_source"
assert_contains \
  "plugin logs the exact post-PatchAll message" \
  'Benheim Eternal Fire 0.1.1 loaded after PatchAll.' \
  "$plugin_source"
patch_line="$(grep -nF 'harmony.PatchAll();' "$plugin_source" | cut -d: -f1)"
load_line="$(grep -nF 'Logger.LogInfo(LoadMessage);' "$plugin_source" | cut -d: -f1)"
[[ "$load_line" -gt "$patch_line" ]] || fail "plugin load message must follow PatchAll"

# The Harmony hooks remain a static contract; the refill boundary itself is
# exercised by the no-Unity C# harness above.
assert_contains "new-format world loads are normalized" "nameof(ZDO.Load)" "$source_file"
assert_contains "legacy world loads are normalized" "nameof(ZDO.LoadOldFormat)" "$source_file"
assert_contains "vanilla-client updates are normalized" "nameof(ZDO.Deserialize)" "$source_file"
assert_contains "the patch only runs on the server" "!ZNet.instance.IsServer()" "$source_file"
assert_contains "the patch writes Valheim's native fuel field" "ZDOVars.s_fuel" "$source_file"
assert_contains \
  "ZDO updates use the tested refill boundary" \
  "RefillPolicy.ShouldRefill" \
  "$source_file"
assert_not_contains \
  "actual refills must each be logged" \
  "LoggedRefills" \
  "$source_file"

assert_contains "standing wood torches are supported" '"piece_groundtorch_wood"' "$supported_file"
assert_contains "hearths are supported" '"hearth"' "$supported_file"
assert_not_contains "smelters stay unchanged" '"smelter"' "$supported_file"
assert_not_contains "blast furnaces stay unchanged" '"blastfurnace"' "$supported_file"
assert_not_contains "eitr refineries stay unchanged" '"eitrrefinery"' "$supported_file"

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
  "$verifier" '2026-08-01T12:00:00-04:00' >/dev/null 2>&1; then
  fail "generic readiness must not satisfy the plugin load gate"
fi

printf '%s\n' \
  'Benheim Eternal Fire 0.1.1 loaded after PatchAll.' \
  > "$tmp_dir/journal.log"
MOCK_JOURNAL_ARGS="$tmp_dir/journal.args" \
MOCK_JOURNAL_LOG="$tmp_dir/journal.log" \
JOURNALCTL_BIN="$tmp_dir/journalctl" \
  "$verifier" '2026-08-01T12:00:00-04:00' >/dev/null
assert_contains \
  "plugin verification is bounded to the current start" \
  '--since 2026-08-01T12:00:00-04:00' \
  "$tmp_dir/journal.args"
assert_contains \
  "verifier requires the plugin's exact load message" \
  'Benheim Eternal Fire 0.1.1 loaded after PatchAll.' \
  "$verifier"

cat > "$tmp_dir/systemctl" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$*" >> "$MOCK_SYSTEMCTL_LOG"
if [[ "$1" == start && "${MOCK_START_FAIL:-0}" == 1 ]]; then
  exit 1
fi
MOCK
cat > "$tmp_dir/wait-ready" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
printf '%s\n' "$1" >> "$MOCK_WAIT_LOG"
if [[ "${MOCK_WAIT_FAIL:-0}" == 1 ]]; then
  exit 1
fi
MOCK
chmod +x "$tmp_dir/systemctl" "$tmp_dir/wait-ready"

run_recovery() {
  MOCK_SYSTEMCTL_LOG="$tmp_dir/systemctl.log" \
  MOCK_WAIT_LOG="$tmp_dir/wait.log" \
  SYSTEMCTL_BIN="$tmp_dir/systemctl" \
  VALHEIM_WAIT_READY="$tmp_dir/wait-ready" \
  VALHEIM_ENV_FILE="$tmp_dir/server.env" \
  VALHEIM_RECOVERY_STARTED_AT='2026-08-01T12:01:00-04:00' \
    "$recovery"
}

printf '%s\n' 'VALHEIM_MODDED=1' > "$tmp_dir/server.env"
: > "$tmp_dir/systemctl.log"
: > "$tmp_dir/wait.log"
run_recovery >/dev/null
assert_contains "recovery selects vanilla launch" 'VALHEIM_MODDED=0' "$tmp_dir/server.env"
assert_contains "recovery starts Valheim" 'start valheim.service' "$tmp_dir/systemctl.log"
assert_contains \
  "recovery readiness is bounded to its start" \
  '2026-08-01T12:01:00-04:00' \
  "$tmp_dir/wait.log"

printf '%s\n' 'VALHEIM_MODDED=1' > "$tmp_dir/server.env"
: > "$tmp_dir/systemctl.log"
: > "$tmp_dir/wait.log"
if MOCK_WAIT_FAIL=1 run_recovery >/dev/null 2>&1; then
  fail "recovery must fail when vanilla readiness fails"
fi

printf '%s\n' 'VALHEIM_MODDED=1' > "$tmp_dir/server.env"
: > "$tmp_dir/systemctl.log"
: > "$tmp_dir/wait.log"
if MOCK_START_FAIL=1 run_recovery >/dev/null 2>&1; then
  fail "recovery must fail when vanilla start fails"
fi
[[ ! -s "$tmp_dir/wait.log" ]] || fail "readiness must not run after failed start"

assert_contains \
  "installer executes the exact plugin gate" \
  '"$work/verify-benheim-eternal-fire" "$started_at"' \
  "$installer"
assert_contains \
  "installer executes verified vanilla recovery" \
  '"$work/recover-valheim-vanilla"' \
  "$installer"

echo "PASS: server Eternal Fire behavior, installer gates, and recovery"
