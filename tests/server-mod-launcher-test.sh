#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
launcher="$repo_root/server/valheim-start"
waiter="$repo_root/server/wait-for-valheim"
test_root=$(mktemp -d "${TMPDIR:-/tmp}/valheim-mod-launcher-test.XXXXXX")
trap 'rm -rf "$test_root"' EXIT

checks=0

pass() {
  checks=$((checks + 1))
  echo "ok $checks - $1"
}

fail() {
  echo "not ok $((checks + 1)) - $1" >&2
  exit 1
}

assert_contains() {
  local label=$1
  local expected=$2
  local file=$3

  grep -Fq -- "$expected" "$file" || fail "$label"
  pass "$label"
}

assert_not_contains() {
  local label=$1
  local unexpected=$2
  local file=$3

  if grep -Fq -- "$unexpected" "$file"; then
    fail "$label"
  fi
  pass "$label"
}

mkdir -p "$test_root/server"
cat > "$test_root/server/valheim_server.x86_64" <<'EOF'
#!/usr/bin/env bash
printf 'args:'
printf ' <%s>' "$@"
printf '\nDOORSTOP_ENABLED=%s\n' "${DOORSTOP_ENABLED:-}"
printf 'DOORSTOP_TARGET_ASSEMBLY=%s\n' "${DOORSTOP_TARGET_ASSEMBLY:-}"
printf 'LD_PRELOAD=%s\n' "${LD_PRELOAD:-}"
EOF
chmod 0755 "$test_root/server/valheim_server.x86_64"

common_env=(
  VALHEIM_SERVER_DIR="$test_root/server"
  VALHEIM_SERVER_NAME="Test Server"
  VALHEIM_WORLD_NAME="TestWorld"
  VALHEIM_PASSWORD="test-password"
)

env "${common_env[@]}" VALHEIM_MODDED=0 "$launcher" > "$test_root/vanilla.out"
assert_contains "vanilla launch keeps the configured world" "<-world> <TestWorld>" "$test_root/vanilla.out"
assert_contains "vanilla launch keeps mods disabled" "DOORSTOP_ENABLED=" "$test_root/vanilla.out"
assert_not_contains "vanilla launch does not preload Doorstop" "libdoorstop_x64.so" "$test_root/vanilla.out"

if env "${common_env[@]}" VALHEIM_MODDED=1 "$launcher" > "$test_root/missing.out" 2>&1; then
  fail "modded launch rejects an incomplete BepInEx install"
fi
assert_contains "incomplete install names the missing preloader" "BepInEx.Preloader.dll is missing" "$test_root/missing.out"

mkdir -p "$test_root/server/BepInEx/core" "$test_root/server/doorstop_libs"
: > "$test_root/server/BepInEx/core/BepInEx.Preloader.dll"
: > "$test_root/server/doorstop_libs/libdoorstop_x64.so"

env "${common_env[@]}" VALHEIM_MODDED=1 "$launcher" > "$test_root/modded.out"
assert_contains "modded launch enables Doorstop" "DOORSTOP_ENABLED=1" "$test_root/modded.out"
assert_contains "modded launch selects the BepInEx preloader" \
  "DOORSTOP_TARGET_ASSEMBLY=./BepInEx/core/BepInEx.Preloader.dll" "$test_root/modded.out"
assert_contains "modded launch preloads the Linux Doorstop library" \
  "LD_PRELOAD=libdoorstop_x64.so" "$test_root/modded.out"

env "${common_env[@]}" VALHEIM_MODDED=0 VALHEIM_PORTALS=casual \
  "$launcher" > "$test_root/portals.out"
assert_contains "casual portal rules use Valheim's native modifier" \
  "<-modifier> <portals> <casual>" "$test_root/portals.out"

env "${common_env[@]}" VALHEIM_MODDED=0 \
  VALHEIM_SKILL_GAIN_RATE=150 VALHEIM_SKILL_REDUCTION_RATE=20 \
  "$launcher" > "$test_root/skill-rates.out"
assert_contains "skill gain uses Valheim's native scalar key" \
  "<-setkey> <skillgainrate 150>" "$test_root/skill-rates.out"
assert_contains "death skill loss uses Valheim's native scalar key" \
  "<-setkey> <skillreductionrate 20>" "$test_root/skill-rates.out"

if env "${common_env[@]}" VALHEIM_MODDED=0 VALHEIM_SKILL_GAIN_RATE=fast \
  "$launcher" > "$test_root/invalid-skill-rate.out" 2>&1; then
  fail "invalid skill rates are rejected"
fi
assert_contains "invalid skill rates explain accepted values" \
  "expected a non-negative percentage or empty" "$test_root/invalid-skill-rate.out"

if env "${common_env[@]}" VALHEIM_MODDED=0 VALHEIM_PORTALS=invalid \
  "$launcher" > "$test_root/invalid-portals.out" 2>&1; then
  fail "invalid portal rules are rejected"
fi
assert_contains "invalid portal rules explain accepted values" \
  "expected casual, hard, veryhard, or empty" "$test_root/invalid-portals.out"

mkdir -p "$test_root/fake-ready-bin"
cat > "$test_root/fake-ready-bin/journalctl" <<'EOF'
#!/usr/bin/env bash
[[ ${READY:-0} == 1 ]] && echo 'Game server connected'
EOF
cat > "$test_root/fake-ready-bin/systemctl" <<'EOF'
#!/usr/bin/env bash
[[ ${SERVICE_ACTIVE:-0} == 1 ]]
EOF
chmod 0755 "$test_root/fake-ready-bin/journalctl" "$test_root/fake-ready-bin/systemctl"

env READY=1 SERVICE_ACTIVE=1 PATH="$test_root/fake-ready-bin:$PATH" \
  "$waiter" "2026-07-31T20:00:00+00:00"
pass "readiness waits for Game server connected"

if env READY=0 SERVICE_ACTIVE=0 PATH="$test_root/fake-ready-bin:$PATH" \
  "$waiter" "2026-07-31T20:00:00+00:00"; then
  fail "readiness fails when the service exits"
fi
pass "readiness fails when the service exits"

echo "1..$checks"
