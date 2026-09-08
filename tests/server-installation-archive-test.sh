#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
helper="$repo_root/server/archive-valheim-installation"
test_root=$(mktemp -d "${TMPDIR:-/tmp}/valheim-server-archive-test.XXXXXX")
trap 'rm -R "$test_root"' EXIT

checks=0
pass() { checks=$((checks + 1)); echo "ok $checks - $1"; }
fail() { echo "not ok $((checks + 1)) - $1" >&2; exit 1; }
expect() { local label=$1; shift; "$@" || fail "$label"; pass "$label"; }

make_fixture() {
  local name=$1 state=$2
  fixture="$test_root/$name"
  fixture_root="$fixture/root"
  fixture_dest="$fixture/backups"
  fixture_state="$fixture/state"
  fixture_upload="$fixture/upload"
  fixture_bin="$fixture/bin"
  mkdir -p "$fixture_root/opt/valheim/server/steamapps" "$fixture_root/usr/local/bin" \
    "$fixture_root/etc/systemd/system" "$fixture_root/etc/valheim" "$fixture_dest" "$fixture_bin"
  printf '%s\n' "$state" > "$fixture_state"
  printf 'server binary\n' > "$fixture_root/opt/valheim/server/valheim_server.x86_64"
  printf '"buildid" "12345678"\n' > "$fixture_root/opt/valheim/server/steamapps/appmanifest_896660.acf"
  printf 'launcher\n' > "$fixture_root/usr/local/bin/valheim-start"
  printf 'waiter\n' > "$fixture_root/usr/local/bin/valheim-wait-ready"
  printf 'unit\n' > "$fixture_root/etc/systemd/system/valheim.service"
  printf 'runtime secret sentinel\n' > "$fixture_root/etc/valheim/server.env"
  printf 'r2 config\n' > "$fixture_root/etc/valheim/r2.env"
  cat > "$fixture_root/usr/local/bin/valheim-r2-upload" <<'EOF'
#!/usr/bin/env bash
printf '%s\n' "$1" > "$ARCHIVE_TEST_UPLOAD"
exit "${ARCHIVE_TEST_UPLOAD_STATUS:-0}"
EOF
  cat > "$fixture_bin/systemctl" <<'EOF'
#!/usr/bin/env bash
[[ $1 == is-active ]] || exit 2
[[ $(cat "$ARCHIVE_TEST_STATE") == active ]]
EOF
  cat > "$fixture_bin/date" <<'EOF'
#!/usr/bin/env bash
printf '20260908T190000Z\n'
EOF
  chmod 0755 "$fixture_root/usr/local/bin/valheim-r2-upload" "$fixture_bin/systemctl" "$fixture_bin/date"
}

run_helper() {
  ARCHIVE_TEST_STATE="$fixture_state" ARCHIVE_TEST_UPLOAD="$fixture_upload" \
  ARCHIVE_TEST_UPLOAD_STATUS="${1:-0}" VALHEIM_ARCHIVE_ROOT="$fixture_root" \
  VALHEIM_ARCHIVE_DEST_DIR="$fixture_dest" PATH="$fixture_bin:$PATH" "$helper"
}

make_fixture running active
if run_helper > "$fixture/out" 2>&1; then fail "running service is refused"; fi
pass "running service is refused"
expect "running refusal creates no archive" test -z "$(ls -A "$fixture_dest")"

make_fixture stopped inactive
run_helper > "$fixture/out"
archive="$fixture_dest/server-installation-20260908T190000Z.tar.gz"
expect "stopped service creates a readable archive" tar -tzf "$archive"
actual_members=$(tar -tzf "$archive" | LC_ALL=C sort)
expect "archive contains the server binary" grep -Fq 'opt/valheim/server/valheim_server.x86_64' <<<"$actual_members"
expect "archive contains the Steam manifest" grep -Fq 'opt/valheim/server/steamapps/appmanifest_896660.acf' <<<"$actual_members"
expect "archive contains the launcher" grep -Fq 'usr/local/bin/valheim-start' <<<"$actual_members"
expect "archive contains the readiness helper" grep -Fq 'usr/local/bin/valheim-wait-ready' <<<"$actual_members"
expect "archive contains the systemd unit" grep -Fq 'etc/systemd/system/valheim.service' <<<"$actual_members"
if grep -Fq 'etc/valheim/server.env' <<<"$actual_members"; then fail "archive excludes the runtime secret file"; fi
pass "archive excludes the runtime secret file"
mode=$(stat -f '%Lp' "$archive" 2>/dev/null || stat -c '%a' "$archive")
expect "archive is mode 0600" test "$mode" = 600
expect "successful archive is sent to the uploader" grep -Fxq "$archive" "$fixture_upload"
expected_sha=$(shasum -a 256 "$archive" | awk '{print $1}')
expect "reported hash matches the archive" grep -Fq "SHA-256: $expected_sha" "$fixture/out"

make_fixture upload-failure inactive
if run_helper 9 > "$fixture/out" 2>&1; then fail "upload failure returns nonzero"; fi
pass "upload failure returns nonzero"
expect "upload failure preserves the local recovery archive" test -f "$fixture_dest/server-installation-20260908T190000Z.tar.gz"

echo "1..$checks"
