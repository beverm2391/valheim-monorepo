#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
helper="$repo_root/server/switch-valheim-world"
wrapper="$repo_root/scripts/switch-world.sh"
test_root=$(mktemp -d "${TMPDIR:-/tmp}/valheim-world-switch-test.XXXXXX")
cleanup_test_root() {
  [[ "$test_root" == "${TMPDIR:-/tmp}/valheim-world-switch-test."* ]] || return
  rm -R "$test_root"
}
trap cleanup_test_root EXIT

checks=0
pass() { checks=$((checks + 1)); echo "ok $checks - $1"; }
fail() { echo "not ok $((checks + 1)) - $1" >&2; exit 1; }
assert_contains() { local label=$1 expected=$2 file=$3; grep -Fq -- "$expected" "$file" || fail "$label"; pass "$label"; }

make_fixture() {
  local name=$1
  fixture="$test_root/$name"
  fixture_root="$fixture/root"
  fixture_bin="$fixture/bin"
  fixture_log="$fixture/actions.log"
  fixture_journal="$fixture/journal.log"
  fixture_state="$fixture/state"
  mkdir -p "$fixture_root/etc/valheim" \
    "$fixture_root/var/lib/valheim/worlds_local" \
    "$fixture_root/var/backups/valheim" \
    "$fixture_root/usr/local/bin" "$fixture_bin"
  printf 'active\n' > "$fixture_state"
  : > "$fixture_log"
  : > "$fixture_journal"
  printf 'VALHEIM_SERVER_NAME=Test\nVALHEIM_WORLD_NAME=OldWorld\nVALHEIM_PASSWORD=test-password\n' \
    > "$fixture_root/etc/valheim/server.env"
  printf 'old database\n' > "$fixture_root/var/lib/valheim/worlds_local/OldWorld.db"
  printf 'old metadata\n' > "$fixture_root/var/lib/valheim/worlds_local/OldWorld.fwl"

  cat > "$fixture_bin/systemctl" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
case "$1" in
  is-active) [[ $(cat "$WORLD_TEST_STATE") == active ]] ;;
  stop) printf 'stop\n' >> "$WORLD_TEST_LOG"; printf 'inactive\n' > "$WORLD_TEST_STATE" ;;
  start)
    printf 'start\n' >> "$WORLD_TEST_LOG"
    world=$(bash -c 'source "$1"; printf "%s" "$VALHEIM_WORLD_NAME"' _ "$WORLD_TEST_ENV")
    printf 'active\n' > "$WORLD_TEST_STATE"
    if [[ ! -e "$WORLD_TEST_WORLDS/$world.db" && ! -e "$WORLD_TEST_WORLDS/$world" ]]; then
      mkdir -p "$WORLD_TEST_WORLDS/$world"
      printf 'new metadata\n' > "$WORLD_TEST_WORLDS/$world/_main.0.fwl2"
    fi
    reported=${REPORT_WORLD_AS:-$world}
    if [[ -d "$WORLD_TEST_WORLDS/$world" ]]; then
      printf 'ZNet.LoadWorld: %s (%s), save number 0\n' "$reported" "$reported" >> "$WORLD_TEST_JOURNAL"
    else
      printf 'Load world: %s (%s)\n' "$reported" "$reported" >> "$WORLD_TEST_JOURNAL"
    fi
    printf 'Game server connected\n' >> "$WORLD_TEST_JOURNAL"
    ;;
  *) exit 2 ;;
esac
EOF
  cat > "$fixture_bin/backup" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
printf 'backup\n' >> "$WORLD_TEST_LOG"
[[ ${FAIL_BACKUP:-0} == 0 ]] || exit 92
stamp=$(date -u +%Y%m%dT%H%M%S%N)
tar -C "$WORLD_TEST_WORLDS" -czf "$WORLD_TEST_BACKUPS/worlds-$stamp.tar.gz" .
EOF
  cat > "$fixture_bin/journalctl" <<'EOF'
#!/usr/bin/env bash
cat "$WORLD_TEST_JOURNAL"
EOF
  cat > "$fixture_bin/date" <<'EOF'
#!/usr/bin/env bash
printf '2026-09-12T12:00:00+00:00\n'
EOF
  cat > "$fixture_root/usr/local/bin/valheim-wait-ready" <<'EOF'
#!/usr/bin/env bash
printf 'ready\n' >> "$WORLD_TEST_LOG"
EOF
  chmod 0755 "$fixture_bin/systemctl" "$fixture_bin/backup" "$fixture_bin/journalctl" \
    "$fixture_bin/date" "$fixture_root/usr/local/bin/valheim-wait-ready"
}

run_helper() {
  env \
    VALHEIM_CONFIG_ROOT="$fixture_root" \
    VALHEIM_BACKUP_COMMAND="$fixture_bin/backup" \
    SYSTEMCTL_BIN="$fixture_bin/systemctl" \
    JOURNALCTL_BIN="$fixture_bin/journalctl" \
    DATE_BIN="$fixture_bin/date" \
    WORLD_TEST_STATE="$fixture_state" \
    WORLD_TEST_LOG="$fixture_log" \
    WORLD_TEST_JOURNAL="$fixture_journal" \
    WORLD_TEST_ENV="$fixture_root/etc/valheim/server.env" \
    WORLD_TEST_WORLDS="$fixture_root/var/lib/valheim/worlds_local" \
    WORLD_TEST_BACKUPS="$fixture_root/var/backups/valheim" \
    "$@"
}

make_fixture existing
printf 'friend database\n' > "$fixture_root/var/lib/valheim/worlds_local/FriendWorld.db"
printf 'friend metadata\n' > "$fixture_root/var/lib/valheim/worlds_local/FriendWorld.fwl"
run_helper "$helper" switch FriendWorld > "$fixture/out"
assert_contains "existing world becomes the runtime selection" 'VALHEIM_WORLD_NAME=FriendWorld' "$fixture_root/etc/valheim/server.env"
[[ $(sed -n '1p' "$fixture_log") == stop && $(sed -n '2p' "$fixture_log") == backup ]] || fail "backup follows the stopped-service snapshot"
pass "backup follows the stopped-service snapshot"
assert_contains "switch restarts the service" 'start' "$fixture_log"
assert_contains "switch waits for readiness" 'ready' "$fixture_log"
assert_contains "switch reports the loaded world" "Valheim loaded world 'FriendWorld'" "$fixture/out"

make_fixture typo
if run_helper "$helper" switch FrendWorld > "$fixture/out" 2>&1; then fail "unknown switch target is rejected"; fi
pass "unknown switch target is rejected"
[[ ! -s "$fixture_log" ]] || fail "unknown switch target performs no service or backup action"
pass "unknown switch target performs no service or backup action"
assert_contains "unknown switch target explains the typo guard" 'refusing to create it from a possible typo' "$fixture/out"

make_fixture create
run_helper "$helper" create NewWorld NewWorld > "$fixture/out"
[[ -s "$fixture_root/var/lib/valheim/worlds_local/NewWorld/_main.0.fwl2" ]] || fail "explicit creation produces a chunked save"
pass "explicit creation produces a chunked save"
assert_contains "explicit creation becomes the runtime selection" 'VALHEIM_WORLD_NAME=NewWorld' "$fixture_root/etc/valheim/server.env"

make_fixture chunked
mkdir -p "$fixture_root/var/lib/valheim/worlds_local/ChunkedWorld"
printf 'chunked metadata\n' > "$fixture_root/var/lib/valheim/worlds_local/ChunkedWorld/_main.1.fwl2"
run_helper "$helper" switch ChunkedWorld > "$fixture/out"
assert_contains "existing chunked world passes the 1.0 log proof" \
  "Valheim loaded world 'ChunkedWorld' from its chunked save" "$fixture/out"

make_fixture confirmation
if run_helper "$helper" create NewWorld NewWrold > "$fixture/out" 2>&1; then fail "mismatched creation confirmation is rejected"; fi
pass "mismatched creation confirmation is rejected"
[[ ! -s "$fixture_log" ]] || fail "mismatched creation confirmation performs no service or backup action"
pass "mismatched creation confirmation performs no service or backup action"

make_fixture collision
if run_helper "$helper" create OldWorld OldWorld > "$fixture/out" 2>&1; then fail "create refuses an existing world"; fi
pass "create refuses an existing world"

make_fixture incomplete
printf 'orphan database\n' > "$fixture_root/var/lib/valheim/worlds_local/Broken.db"
if run_helper "$helper" switch Broken > "$fixture/out" 2>&1; then fail "incomplete save is rejected"; fi
pass "incomplete save is rejected"
[[ ! -s "$fixture_log" ]] || fail "incomplete save performs no service or backup action"
pass "incomplete save performs no service or backup action"

make_fixture wrong-log
printf 'friend database\n' > "$fixture_root/var/lib/valheim/worlds_local/FriendWorld.db"
printf 'friend metadata\n' > "$fixture_root/var/lib/valheim/worlds_local/FriendWorld.fwl"
if REPORT_WORLD_AS=OtherWorld run_helper "$helper" switch FriendWorld > "$fixture/out" 2>&1; then
  fail "loaded-world mismatch fails the switch"
fi
pass "loaded-world mismatch fails the switch"
assert_contains "loaded-world mismatch restores the prior runtime selection" 'VALHEIM_WORLD_NAME=OldWorld' "$fixture_root/etc/valheim/server.env"
assert_contains "loaded-world mismatch reports rollback" 'restoring the previous runtime configuration' "$fixture/out"

make_fixture backup-failure
printf 'friend database\n' > "$fixture_root/var/lib/valheim/worlds_local/FriendWorld.db"
printf 'friend metadata\n' > "$fixture_root/var/lib/valheim/worlds_local/FriendWorld.fwl"
if FAIL_BACKUP=1 run_helper "$helper" switch FriendWorld > "$fixture/out" 2>&1; then
  fail "backup failure blocks the switch"
fi
pass "backup failure blocks the switch"
assert_contains "backup failure preserves the prior runtime selection" 'VALHEIM_WORLD_NAME=OldWorld' "$fixture_root/etc/valheim/server.env"
[[ $(cat "$fixture_state") == active ]] || fail "backup failure restores the prior active service"
pass "backup failure restores the prior active service"

make_fixture ambiguous
mkdir -p "$fixture_root/var/lib/valheim/worlds_local/OldWorld"
printf 'chunked metadata\n' > "$fixture_root/var/lib/valheim/worlds_local/OldWorld/_main.0.fwl2"
if run_helper "$helper" switch OldWorld > "$fixture/out" 2>&1; then fail "ambiguous primary saves are rejected"; fi
pass "ambiguous primary saves are rejected"
[[ ! -s "$fixture_log" ]] || fail "ambiguous save performs no service or backup action"
pass "ambiguous save performs no service or backup action"

wrapper_fixture="$test_root/wrapper"
wrapper_bin="$wrapper_fixture/bin"
mkdir -p "$wrapper_bin"
cat > "$wrapper_fixture/server.env" <<'EOF'
HETZNER_SERVER_NAME=test-server
VALHEIM_SERVER_NAME=Test
VALHEIM_WORLD_NAME=OldWorld
SSH_HOST=test-host
SSH_USER=root
EOF
cat > "$wrapper_bin/scp" <<'EOF'
#!/usr/bin/env bash
printf 'scp <%s>\n' "$*" >> "$WRAPPER_LOG"
EOF
cat > "$wrapper_bin/ssh" <<'EOF'
#!/usr/bin/env bash
printf 'ssh <%s>\n' "$*" >> "$WRAPPER_LOG"
if [[ -n ${WRAPPER_FAIL_COMMAND:-} && $* == *"$WRAPPER_FAIL_COMMAND"* ]]; then
  exit 93
fi
EOF
chmod 0755 "$wrapper_bin/scp" "$wrapper_bin/ssh"
: > "$wrapper_fixture/remote.log"
WRAPPER_LOG="$wrapper_fixture/remote.log" PATH="$wrapper_bin:$PATH" \
  VALHEIM_ENV_FILE="$wrapper_fixture/server.env" "$wrapper" FriendWorld > "$wrapper_fixture/out"
assert_contains "wrapper transfers the remote switch helper" 'server/switch-valheim-world' "$wrapper_fixture/remote.log"
assert_contains "wrapper requests an existing-world switch" 'switch FriendWorld' "$wrapper_fixture/remote.log"
assert_contains "verified wrapper updates local operator configuration" 'VALHEIM_WORLD_NAME=FriendWorld' "$wrapper_fixture/server.env"

: > "$wrapper_fixture/remote.log"
if WRAPPER_FAIL_COMMAND='switch FailWorld' WRAPPER_LOG="$wrapper_fixture/remote.log" \
  PATH="$wrapper_bin:$PATH" VALHEIM_ENV_FILE="$wrapper_fixture/server.env" \
  "$wrapper" FailWorld > "$wrapper_fixture/out" 2>&1; then
  fail "remote verification failure reaches the wrapper"
fi
pass "remote verification failure reaches the wrapper"
assert_contains "remote failure leaves local operator configuration unchanged" \
  'VALHEIM_WORLD_NAME=FriendWorld' "$wrapper_fixture/server.env"

: > "$wrapper_fixture/remote.log"
if WRAPPER_LOG="$wrapper_fixture/remote.log" PATH="$wrapper_bin:$PATH" \
  VALHEIM_ENV_FILE="$wrapper_fixture/server.env" "$wrapper" --create-new NewWorld --confirm NewWrold \
  > "$wrapper_fixture/out" 2>&1; then
  fail "wrapper rejects a mismatched creation confirmation"
fi
pass "wrapper rejects a mismatched creation confirmation"
[[ ! -s "$wrapper_fixture/remote.log" ]] || fail "invalid creation syntax makes no remote call"
pass "invalid creation syntax makes no remote call"

echo "1..$checks"
