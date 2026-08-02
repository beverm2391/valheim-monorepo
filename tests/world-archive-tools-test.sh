#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
inspect="$repo_root/scripts/inspect-world-archive.sh"
restore="$repo_root/scripts/restore-world-archive.sh"
test_root=$(mktemp -d "${TMPDIR:-/tmp}/valheim-world-tools-test.XXXXXX")
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

assert_fails() {
  local label=$1
  shift

  if "$@" > "$test_root/failure.out" 2>&1; then
    fail "$label"
  fi
  pass "$label"
}

mkdir -p "$test_root/legacy"
printf 'legacy database\n' > "$test_root/legacy/FriendWorld.db"
printf 'legacy metadata\n' > "$test_root/legacy/FriendWorld.fwl"
tar -C "$test_root/legacy" -czf "$test_root/legacy.tar.gz" .

mkdir -p "$test_root/server/steamapps"
cat > "$test_root/server/steamapps/appmanifest_896660.acf" <<'EOF'
"AppState"
{
  "appid" "896660"
  "name" "Valheim Dedicated Server"
  "buildid" "12345678"
  "LastUpdated" "1770000000"
}
EOF

"$inspect" "$test_root/legacy.tar.gz" "$test_root/server" > "$test_root/legacy.inspect"
assert_contains "inspector recognizes legacy storage" "Storage layout: legacy files" "$test_root/legacy.inspect"
assert_contains "inspector hashes legacy metadata" "FriendWorld.fwl" "$test_root/legacy.inspect"
assert_contains "inspector reports the Steam build id" "buildid: 12345678" "$test_root/legacy.inspect"

legacy_destination="$test_root/legacy-restore/worlds_local"
mkdir -p "$(dirname "$legacy_destination")"
"$restore" --local "$test_root/legacy.tar.gz" "$legacy_destination" > "$test_root/legacy.restore"
[[ -f "$legacy_destination/FriendWorld.db" && -f "$legacy_destination/FriendWorld.fwl" ]] || fail "restore installs legacy world pairs"
pass "restore installs legacy world pairs"

mkdir -p "$test_root/chunked/FriendWorld/chunks" "$test_root/chunked/FriendWorld_backups/save-1"
printf 'world metadata\n' > "$test_root/chunked/FriendWorld/metadata"
printf 'chunk zero\n' > "$test_root/chunked/FriendWorld/chunks/0.bin"
printf 'backup chunk\n' > "$test_root/chunked/FriendWorld_backups/save-1/0.bin"
tar -C "$test_root/chunked" -czf "$test_root/chunked.tar.gz" .

destination="$test_root/var/lib/valheim/worlds_local"
mkdir -p "$destination"
printf 'old world\n' > "$destination/OldWorld.db"
"$restore" --local "$test_root/chunked.tar.gz" "$destination" > "$test_root/restore.out"

[[ -f "$destination/FriendWorld/chunks/0.bin" ]] || fail "restore installs directory-based storage"
[[ ! -e "$destination/OldWorld.db" ]] || fail "restore does not mix the old layout into the replacement"
pass "restore installs directory-based storage without mixing layouts"

shopt -s nullglob
quarantines=("$destination".quarantine-*)
shopt -u nullglob
[[ ${#quarantines[@]} -eq 1 && -f "${quarantines[0]}/OldWorld.db" ]] || fail "restore quarantines the previous directory"
pass "restore quarantines the previous directory"

"$inspect" "$destination" > "$test_root/chunked.inspect"
assert_contains "inspector recognizes directory-based storage" "Storage layout: directory-based" "$test_root/chunked.inspect"
assert_contains "inspector reports future metadata candidates without parsing them" "FriendWorld/metadata" "$test_root/chunked.inspect"

guard_destination="$test_root/guard/worlds_local"
mkdir -p "$guard_destination"
printf 'must survive\n' > "$guard_destination/current.db"

non_world_destination="$test_root/guard/not-world-storage"
mkdir -p "$non_world_destination"
printf 'must also survive\n' > "$non_world_destination/current.db"
assert_fails "a destination not named worlds_local is rejected" \
  "$restore" --local "$test_root/legacy.tar.gz" "$non_world_destination"
[[ -f "$non_world_destination/current.db" ]] || fail "invalid destination preserves current storage"
pass "invalid destination preserves current storage"

assert_fails "SHA mismatch blocks replacement" \
  "$restore" --local "$test_root/legacy.tar.gz" "$guard_destination" --expected-sha256 deadbeef
[[ -f "$guard_destination/current.db" ]] || fail "SHA mismatch preserves current storage"
pass "SHA mismatch preserves current storage"

printf 'not a tar archive\n' > "$test_root/corrupt.tar.gz"
assert_fails "corrupt archives are rejected" "$restore" --local "$test_root/corrupt.tar.gz" "$guard_destination"
[[ -f "$guard_destination/current.db" ]] || fail "corrupt archive preserves current storage"
pass "corrupt archive preserves current storage"

mkdir -p "$test_root/empty"
tar -C "$test_root/empty" -czf "$test_root/empty.tar.gz" .
assert_fails "empty archives are rejected" "$inspect" --validate-only "$test_root/empty.tar.gz"

mkdir -p "$test_root/unsafe-source"
printf 'escape attempt\n' > "$test_root/unsafe-source/safe"
if tar -C "$test_root/unsafe-source" -czf "$test_root/unsafe.tar.gz" -s '|^safe$|../escape|' safe 2>/dev/null; then
  :
elif tar -C "$test_root/unsafe-source" -czf "$test_root/unsafe.tar.gz" --transform='s|^safe$|../escape|' safe 2>/dev/null; then
  :
else
  fail "test harness can construct an unsafe archive"
fi
assert_fails "parent-directory archive paths are rejected" \
  "$inspect" --validate-only "$test_root/unsafe.tar.gz"

mkdir -p "$test_root/symlink-source"
ln -s ../outside "$test_root/symlink-source/escape-link"
tar -C "$test_root/symlink-source" -czf "$test_root/symlink.tar.gz" .
assert_fails "symlink archive members are rejected by inspection" \
  "$inspect" --validate-only "$test_root/symlink.tar.gz"
assert_fails "symlink archive members are rejected before restore" \
  "$restore" --local "$test_root/symlink.tar.gz" "$guard_destination"
[[ -f "$guard_destination/current.db" ]] || fail "symlink archive preserves current storage"
pass "symlink archive preserves current storage"

mkdir -p "$test_root/wrapped-source/worlds_local"
printf 'wrapped world\n' > "$test_root/wrapped-source/worlds_local/FriendWorld.db"
tar -C "$test_root/wrapped-source" -czf "$test_root/wrapped.tar.gz" worlds_local
assert_fails "a top-level worlds_local wrapper is rejected by inspection" \
  "$inspect" --validate-only "$test_root/wrapped.tar.gz"
assert_contains "wrapper rejection explains the contents-root contract" \
  "archive its contents instead" "$test_root/failure.out"
assert_fails "a top-level worlds_local wrapper is rejected before restore" \
  "$restore" --local "$test_root/wrapped.tar.gz" "$guard_destination"
[[ -f "$guard_destination/current.db" ]] || fail "wrapped archive preserves current storage"
pass "wrapped archive preserves current storage"

failure_destination="$test_root/failure-injection/worlds_local"
mkdir -p "$failure_destination" "$test_root/fake-mv-bin"
printf 'original world\n' > "$failure_destination/OriginalWorld.db"
printf '0\n' > "$test_root/mv-count"
cat > "$test_root/fake-mv-bin/mv" <<'EOF'
#!/usr/bin/env bash
set -euo pipefail
count=$(cat "$MV_COUNT_FILE")
count=$((count + 1))
printf '%s\n' "$count" > "$MV_COUNT_FILE"
if [[ $count -eq 2 ]]; then
  exit 99
fi
exec "$REAL_MV" "$@"
EOF
chmod 0755 "$test_root/fake-mv-bin/mv"
assert_fails "a failed staged replacement restores the quarantined world" \
  env MV_COUNT_FILE="$test_root/mv-count" REAL_MV="$(command -v mv)" \
    PATH="$test_root/fake-mv-bin:$PATH" \
    "$restore" --local "$test_root/legacy.tar.gz" "$failure_destination"
[[ -f "$failure_destination/OriginalWorld.db" ]] || fail "rollback restores the original world"
[[ ! -e "$failure_destination/FriendWorld.db" ]] || fail "failed replacement does not install staged data"
[[ $(cat "$test_root/mv-count") -eq 3 ]] || fail "rollback performs the quarantine restore move"
shopt -s nullglob
failure_quarantines=("$failure_destination".quarantine-*)
shopt -u nullglob
[[ ${#failure_quarantines[@]} -eq 0 ]] || fail "rollback consumes the quarantine directory"
pass "failed replacement leaves the original worlds_local intact"

mkdir -p "$test_root/fake-active-bin"
cat > "$test_root/fake-active-bin/systemctl" <<'EOF'
#!/usr/bin/env bash
[[ ${1:-} == "is-active" ]]
EOF
chmod 0755 "$test_root/fake-active-bin/systemctl"
assert_fails "an active Valheim service blocks replacement" \
  env PATH="$test_root/fake-active-bin:$PATH" "$restore" --local "$test_root/legacy.tar.gz" "$guard_destination"
[[ -f "$guard_destination/current.db" ]] || fail "active service guard preserves current storage"
pass "active service guard preserves current storage"

ln -s "$guard_destination" "$test_root/worlds-link"
assert_fails "a symlink destination is rejected" \
  "$restore" --local "$test_root/legacy.tar.gz" "$test_root/worlds-link"

mkdir -p "$test_root/fake-remote-bin"
cat > "$test_root/fake-remote-bin/scp" <<'EOF'
#!/usr/bin/env bash
printf 'scp' >> "$COMMAND_LOG"
printf ' <%s>' "$@" >> "$COMMAND_LOG"
printf '\n' >> "$COMMAND_LOG"
EOF
cat > "$test_root/fake-remote-bin/ssh" <<'EOF'
#!/usr/bin/env bash
printf 'ssh' >> "$COMMAND_LOG"
printf ' <%s>' "$@" >> "$COMMAND_LOG"
printf '\n' >> "$COMMAND_LOG"
EOF
chmod 0755 "$test_root/fake-remote-bin/scp" "$test_root/fake-remote-bin/ssh"

cat > "$test_root/server.env" <<'EOF'
HETZNER_SERVER_NAME=test-server
VALHEIM_SERVER_NAME=Test-Server
VALHEIM_WORLD_NAME=FriendWorld
VALHEIM_PASSWORD=test-only-password
SSH_HOST=test-host
SSH_USER=root
EOF

: > "$test_root/remote-commands.log"
COMMAND_LOG="$test_root/remote-commands.log" \
VALHEIM_ENV_FILE="$test_root/server.env" \
PATH="$test_root/fake-remote-bin:$PATH" \
  "$restore" "$test_root/legacy.tar.gz" > "$test_root/remote.out"

[[ $(grep -c '^scp' "$test_root/remote-commands.log") -eq 3 ]] || fail "remote wrapper uploads the archive and two tools"
pass "remote wrapper uploads the archive and two tools"
assert_contains "remote wrapper targets the complete worlds_local directory" "/var/lib/valheim/worlds_local" "$test_root/remote-commands.log"
assert_contains "remote wrapper stops Valheim before replacement" "systemctl stop valheim.service" "$test_root/remote-commands.log"
assert_contains "remote wrapper carries the local archive checksum" "--expected-sha256" "$test_root/remote-commands.log"
assert_contains "remote wrapper inspects the installed server build" "/opt/valheim/server" "$test_root/remote-commands.log"

echo "1..$checks"
