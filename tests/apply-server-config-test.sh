#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
helper="$repo_root/server/apply-valheim-config"
test_root=$(mktemp -d "${TMPDIR:-/tmp}/valheim-config-apply-test.XXXXXX")
trap 'rm -R "$test_root"' EXIT

checks=0
pass() { checks=$((checks + 1)); echo "ok $checks - $1"; }
fail() { echo "not ok $((checks + 1)) - $1" >&2; exit 1; }
expect() { local label=$1; shift; "$@" || fail "$label"; pass "$label"; }

make_fixture() {
  local name=$1
  local state=$2
  fixture="$test_root/$name"
  fixture_root="$fixture/root"
  fixture_work="$fixture/work"
  fixture_state="$fixture/state"
  fixture_log="$fixture/log"
  fixture_bin="$fixture/bin"
  mkdir -p "$fixture_root/etc/valheim" "$fixture_root/usr/local/bin" "$fixture_work" "$fixture_bin"
  printf '%s\n' "$state" > "$fixture_state"
  : > "$fixture_log"
  printf 'old env\n' > "$fixture_root/etc/valheim/server.env"
  printf 'old launcher\n' > "$fixture_root/usr/local/bin/valheim-start"
  printf 'old waiter\n' > "$fixture_root/usr/local/bin/valheim-wait-ready"
  printf 'new env\n' > "$fixture_work/server.env"
  printf 'new launcher\n' > "$fixture_work/valheim-start"
  cat > "$fixture_work/wait-for-valheim" <<'EOF'
#!/usr/bin/env bash
printf 'ready %s\n' "$*" >> "$CONFIG_TEST_LOG"
EOF
  chmod 0755 "$fixture_work/wait-for-valheim"
  cat > "$fixture_bin/systemctl" <<'EOF'
#!/usr/bin/env bash
case "$1" in
  is-active) [[ $(cat "$CONFIG_TEST_STATE") == active ]] ;;
  stop) printf 'inactive\n' > "$CONFIG_TEST_STATE"; printf 'stop\n' >> "$CONFIG_TEST_LOG" ;;
  start) printf 'active\n' > "$CONFIG_TEST_STATE"; printf 'start\n' >> "$CONFIG_TEST_LOG" ;;
  *) exit 2 ;;
esac
EOF
  cat > "$fixture_bin/backup" <<'EOF'
#!/usr/bin/env bash
printf 'backup\n' >> "$CONFIG_TEST_LOG"
EOF
  cat > "$fixture_bin/date" <<'EOF'
#!/usr/bin/env bash
printf '2026-09-08T18:00:00+00:00\n'
EOF
  chmod 0755 "$fixture_bin/systemctl" "$fixture_bin/backup" "$fixture_bin/date"
}

run_helper() {
  local mode=$1
  CONFIG_TEST_STATE="$fixture_state" \
  CONFIG_TEST_LOG="$fixture_log" \
  VALHEIM_CONFIG_ROOT="$fixture_root" \
  VALHEIM_BACKUP_COMMAND="$fixture_bin/backup" \
  PATH="$fixture_bin:$PATH" \
    "$helper" "$mode" "$fixture_work"
}

make_fixture frozen-success active
run_helper 1 > "$fixture/out"
expect "keep-stopped installs the new runtime environment" grep -Fq 'new env' "$fixture_root/etc/valheim/server.env"
expect "keep-stopped leaves an active service stopped" grep -Fq 'inactive' "$fixture_state"
expect "keep-stopped does not start the service" test "$(grep -c '^start$' "$fixture_log" || true)" -eq 0
expect "keep-stopped does not call readiness" test "$(grep -c '^ready ' "$fixture_log" || true)" -eq 0
expect "keep-stopped still creates a frozen backup" grep -Fq 'backup' "$fixture_log"

make_fixture normal-success inactive
run_helper 0 > "$fixture/out"
expect "normal deployment starts the service" grep -Fq 'active' "$fixture_state"
expect "normal deployment waits for readiness" grep -Fq 'ready 2026-09-08T18:00:00+00:00' "$fixture_log"

make_fixture rollback-stopped inactive
rm "$fixture_work/wait-for-valheim"
if run_helper 1 > "$fixture/out" 2>&1; then fail "failed frozen deployment returns nonzero"; fi
pass "failed frozen deployment returns nonzero"
expect "failed frozen deployment restores the prior environment" grep -Fq 'old env' "$fixture_root/etc/valheim/server.env"
expect "failed frozen deployment remains stopped" grep -Fq 'inactive' "$fixture_state"

make_fixture rollback-active active
rm "$fixture_work/wait-for-valheim"
if run_helper 1 > "$fixture/out" 2>&1; then fail "failed active deployment returns nonzero"; fi
pass "failed active deployment returns nonzero"
expect "failed active deployment restores the prior environment" grep -Fq 'old env' "$fixture_root/etc/valheim/server.env"
expect "failed active deployment restores the active state" grep -Fq 'active' "$fixture_state"

echo "1..$checks"
