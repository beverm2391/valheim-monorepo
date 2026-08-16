#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
installer="$root/scripts/install-server-mods.sh"
source_file="$root/server-mods/benheim-eternal-fire/src/ZdoFuelPatches.cs"
supported_file="$root/server-mods/benheim-eternal-fire/src/SupportedFireplaces.cs"
plugin_source="$root/server-mods/benheim-eternal-fire/src/Plugin.cs"
plugin="$root/server-mods/benheim-eternal-fire/dist/BenheimEternalFire.dll"
test_commands_source="$root/server-mods/benheim-test-commands/src/Plugin.cs"
test_commands_plugin="$root/server-mods/benheim-test-commands/dist/BenheimTestCommands.dll"
test_commands_build="$root/server-mods/benheim-test-commands/scripts/build.sh"
server_support_source="$root/server-mods/benheim-server-support/src/Plugin.cs"
server_support_plugin="$root/server-mods/benheim-server-support/dist/BenheimServerSupport.dll"
server_support_build="$root/server-mods/benheim-server-support/scripts/build.sh"
verifier="$root/server/verify-benheim-server-plugins"
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
bash -n "$test_commands_build"
bash -n "$server_support_build"
bash -n "$verifier"
bash -n "$recovery"

dotnet run --project "$root/tests/refill-policy/RefillPolicyTests.csproj" \
  --configuration Release

expected_checksum=8f452cc68d839b7a843676c89b479e357c2b932db8f0f02106de5c5cfde451f4
actual_checksum="$(shasum -a 256 "$plugin" | awk '{print $1}')"
[[ "$actual_checksum" == "$expected_checksum" ]] || fail "first-party plugin checksum changed"
assert_contains "installer pins the first-party plugin checksum" "$expected_checksum" "$installer"
test_commands_checksum=c7630858ebba095709cdcbaacdb96c48f531afc8c1e81dfdbabc3e94cf0c0fe4
actual_test_commands_checksum="$(shasum -a 256 "$test_commands_plugin" | awk '{print $1}')"
[[ "$actual_test_commands_checksum" == "$test_commands_checksum" ]] || fail "test-command plugin checksum changed"
assert_contains "installer pins the test-command plugin checksum" "$test_commands_checksum" "$installer"
server_support_checksum=77a3a3f21e761b0709eefd74e0fb50d9c04b576d3e1c3cb9438994a54a6ce0df
actual_server_support_checksum="$(shasum -a 256 "$server_support_plugin" | awk '{print $1}')"
[[ "$actual_server_support_checksum" == "$server_support_checksum" ]] || fail "server-support plugin checksum changed"
assert_contains "installer pins the server-support plugin checksum" "$server_support_checksum" "$installer"
assert_contains "installer builds Eternal Fire before staging" 'server-mods/benheim-eternal-fire/scripts/build.sh' "$installer"
assert_contains "installer builds Test Commands before staging" 'server-mods/benheim-test-commands/scripts/build.sh' "$installer"
assert_contains "installer builds Server Support before staging" 'server-mods/benheim-server-support/scripts/build.sh' "$installer"
assert_contains "Test Commands build produces its staged artifact" 'dist/BenheimTestCommands.dll' "$test_commands_build"
assert_contains "Server Support build produces its staged artifact" 'dist/BenheimServerSupport.dll' "$server_support_build"
assert_contains "installer replaces the complete plugin set" 'rm -rf /opt/valheim/server/BepInEx/plugins' "$installer"
assert_contains "installer owns an Eternal Fire namespace" "/BepInEx/plugins/BenheimEternalFire" "$installer"
assert_contains "installer owns a Test Commands namespace" "/BepInEx/plugins/BenheimTestCommands" "$installer"
assert_contains "installer owns a Server Support namespace" "/BepInEx/plugins/BenheimServerSupport" "$installer"
assert_not_contains "installer must not auto-discover server mods" 'server-mods/*' "$installer"
assert_not_contains "installer must not add a remote env-file config read" 'source /etc/valheim/server.env' "$installer"
assert_contains "installer passes the already-loaded world into verification" "printf -v expected_world_arg '%q'" "$installer"
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
assert_contains "test-command source pins version 0.1.0" 'PluginVersion = "0.1.0"' "$test_commands_source"
assert_contains "server-support source pins version 0.1.0" 'PluginVersion = "0.1.0"' "$server_support_source"
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
if [[ "${MOCK_JOURNAL_DELAY:-0}" == 1 ]]; then
  count="$(cat "$MOCK_JOURNAL_COUNT")"
  count=$((count + 1))
  printf '%s\n' "$count" > "$MOCK_JOURNAL_COUNT"
  if (( count < 3 )); then
    exit 0
  fi
fi
cat "$MOCK_JOURNAL_LOG"
MOCK
chmod +x "$tmp_dir/journalctl"

cat > "$tmp_dir/systemctl-verify" <<'MOCK'
#!/usr/bin/env bash
set -euo pipefail
if [[ "$1" == show ]]; then
  printf '%s\n' "${MOCK_ACTIVE_INVOCATION:-invocation-a}"
  exit 0
fi
[[ "$1" == is-active && "$2" == --quiet && "$3" == valheim.service ]]
MOCK
chmod +x "$tmp_dir/systemctl-verify"

printf '%s\n' 'Game server connected' > "$tmp_dir/journal.log"
if MOCK_JOURNAL_ARGS="$tmp_dir/journal.args" \
  MOCK_JOURNAL_LOG="$tmp_dir/journal.log" \
  JOURNALCTL_BIN="$tmp_dir/journalctl" \
  SYSTEMCTL_BIN="$tmp_dir/systemctl-verify" \
  SLEEP_BIN=true \
  "$verifier" invocation-a first >/dev/null 2>&1; then
  fail "generic readiness must not satisfy the plugin load gate"
fi

printf '%s\n' \
  'Load world: first (first)' \
  'Game server connected' \
  'Benheim Eternal Fire 0.1.1 loaded after PatchAll.' \
  'Benheim Test Commands 0.1.0 loaded with direct peer RPC authorization.' \
  'Benheim Server Support 0.1.0 loaded with the Put Away lease coordinator.' \
  > "$tmp_dir/journal.log"
printf '%s\n' 0 > "$tmp_dir/journal.count"
MOCK_JOURNAL_ARGS="$tmp_dir/journal.args" \
MOCK_JOURNAL_LOG="$tmp_dir/journal.log" \
MOCK_JOURNAL_DELAY=1 \
MOCK_JOURNAL_COUNT="$tmp_dir/journal.count" \
JOURNALCTL_BIN="$tmp_dir/journalctl" \
SYSTEMCTL_BIN="$tmp_dir/systemctl-verify" \
SLEEP_BIN=true \
  "$verifier" invocation-a first >/dev/null
[[ "$(cat "$tmp_dir/journal.count")" == 3 ]] || fail "verifier must poll until one invocation is fully ready"
assert_contains \
  "plugin verification is bounded to one systemd invocation" \
  '_SYSTEMD_INVOCATION_ID=invocation-a' \
  "$tmp_dir/journal.args"
if MOCK_ACTIVE_INVOCATION=invocation-b \
  MOCK_JOURNAL_ARGS="$tmp_dir/journal.args" \
  MOCK_JOURNAL_LOG="$tmp_dir/journal.log" \
  JOURNALCTL_BIN="$tmp_dir/journalctl" \
  SYSTEMCTL_BIN="$tmp_dir/systemctl-verify" \
  SLEEP_BIN=true \
  "$verifier" invocation-a first >/dev/null 2>&1; then
  fail "verifier must reject an automatic restart into a different invocation"
fi
assert_contains \
  "verifier requires Eternal Fire's exact load message" \
  'Benheim Eternal Fire 0.1.1 loaded after PatchAll.' \
  "$verifier"
assert_contains \
  "verifier requires Test Commands' exact load message" \
  'Benheim Test Commands 0.1.0 loaded with direct peer RPC authorization.' \
  "$verifier"
assert_contains \
  "verifier requires Server Support's exact load message" \
  'Benheim Server Support 0.1.0 loaded with the Put Away lease coordinator.' \
  "$verifier"
assert_contains "verifier requires the configured world" 'Load world: $world ($world)' "$verifier"
assert_contains "verifier requires normal readiness" 'Game server connected' "$verifier"

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
  "installer executes the whole-stack gate" \
  '"$work/verify-benheim-server-plugins" "$invocation_id" "$expected_world"' \
  "$installer"
assert_contains \
  "installer captures the exact started invocation" \
  'systemctl show --property=InvocationID --value valheim.service' \
  "$installer"
assert_contains \
  "installer executes verified vanilla recovery" \
  '"$work/recover-valheim-vanilla"' \
  "$installer"

echo "PASS: approved server plugin stack, Eternal Fire behavior, and recovery"
