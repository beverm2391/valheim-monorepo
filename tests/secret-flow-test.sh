#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
lib="$repo_root/scripts/lib.sh"
test_root=$(mktemp -d "${TMPDIR:-/tmp}/valheim-secret-flow-test.XXXXXX")
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

assert_not_contains() {
  local label=$1
  local needle=$2
  shift 2

  if grep -Fq -- "$needle" "$@"; then
    fail "$label"
  fi
  pass "$label"
}

assert_contains() {
  local label=$1
  local needle=$2
  local file=$3

  grep -Fq -- "$needle" "$file" || fail "$label"
  pass "$label"
}

cat > "$test_root/base.env" <<'EOF'
HETZNER_SERVER_NAME=test-server
VALHEIM_SERVER_NAME="Test Server"
VALHEIM_WORLD_NAME=TestWorld
VALHEIM_PORT=2456
SSH_HOST=test-host
SSH_USER=root
VALHEIM_R2_CONFIGURE=0
VALHEIM_R2_ACCOUNT_ID=
VALHEIM_R2_BUCKET=
VALHEIM_R2_PREFIX=
EOF

secret_keys=(
  HETZNER_TOKEN
  HCLOUD_TOKEN
  TAILSCALE_AUTHKEY
  VALHEIM_PASSWORD
  VALHEIM_R2_ACCESS_KEY_ID
  VALHEIM_R2_SECRET_ACCESS_KEY
)
for key in "${secret_keys[@]}"; do
  config="$test_root/$key.env"
  cp "$test_root/base.env" "$config"
  printf '%s=%s\n' "$key" config-secret-sentinel >> "$config"
  if VALHEIM_ENV_FILE="$config" bash -c 'source "$1"; load_config' _ "$lib" > "$test_root/$key.out" 2>&1; then
    fail "secret assignment $key is rejected before sourcing"
  fi
  grep -Fq "Secret assignment $key is not allowed" "$test_root/$key.out" || fail "secret assignment $key names the rejected key"
  assert_not_contains "secret assignment $key does not print its value" config-secret-sentinel "$test_root/$key.out"
done

bypass_assignments=(
  'declare -x VALHEIM_PASSWORD=config-secret-sentinel'
  'readonly VALHEIM_PASSWORD=config-secret-sentinel'
  'VALHEIM_PASSWORD+=config-secret-sentinel'
  'key=VALHEIM_PASSWORD; printf -v "$key" %s config-secret-sentinel'
)
for index in "${!bypass_assignments[@]}"; do
  config="$test_root/bypass-$index.env"
  cp "$test_root/base.env" "$config"
  printf '%s\n' "${bypass_assignments[$index]}" >> "$config"
  if VALHEIM_ENV_FILE="$config" bash -c 'source "$1"; load_config' _ "$lib" > "$test_root/bypass-$index.out" 2>&1; then
    fail "indirect secret assignment $index is rejected after sourcing"
  fi
  assert_not_contains "indirect secret assignment $index does not print its value" \
    config-secret-sentinel "$test_root/bypass-$index.out"
done

VALHEIM_ENV_FILE="$test_root/base.env" \
VALHEIM_PASSWORD=server-secret-sentinel \
  bash -c 'source "$1"; load_config; render_server_env "$2"' _ "$lib" "$test_root/server.render" > "$test_root/server.render.out" 2>&1
grep -Fq 'VALHEIM_PASSWORD=server-secret-sentinel' "$test_root/server.render" || fail "server artifact receives the process password"
[[ $(stat -f '%Lp' "$test_root/server.render" 2>/dev/null || stat -c '%a' "$test_root/server.render") == 600 ]] || fail "server artifact is mode 0600"
pass "server artifact receives only process password"
assert_not_contains "server rendering does not print the password" server-secret-sentinel "$test_root/server.render.out"
if ! bash -c 'source "$1"; [[ "$VALHEIM_SERVER_NAME" == "Test Server" ]]' _ "$test_root/server.render"; then
  fail "rendered server settings preserve shell-sensitive values"
fi
pass "rendered server settings preserve shell-sensitive values"

cat > "$test_root/r2.env" <<'EOF'
HETZNER_SERVER_NAME=test-server
VALHEIM_SERVER_NAME=Test-Server
VALHEIM_WORLD_NAME=TestWorld
VALHEIM_PORT=2456
SSH_HOST=test-host
SSH_USER=root
VALHEIM_R2_CONFIGURE=1
VALHEIM_R2_ACCOUNT_ID=account-id
VALHEIM_R2_BUCKET=valheim-backups
VALHEIM_R2_PREFIX=
EOF

VALHEIM_ENV_FILE="$test_root/r2.env" \
VALHEIM_PASSWORD=server-secret-sentinel \
VALHEIM_R2_ACCESS_KEY_ID=r2-access-sentinel \
VALHEIM_R2_SECRET_ACCESS_KEY=r2-secret-sentinel \
  bash -c 'source "$1"; load_config; render_r2_env "$2"' _ "$lib" "$test_root/r2.render" > "$test_root/r2.render.out" 2>&1
grep -Fq 'VALHEIM_R2_ACCESS_KEY_ID=r2-access-sentinel' "$test_root/r2.render" || fail "R2 artifact receives the process access key"
grep -Fq 'VALHEIM_R2_SECRET_ACCESS_KEY=r2-secret-sentinel' "$test_root/r2.render" || fail "R2 artifact receives the process secret key"
grep -Fq 'VALHEIM_R2_PREFIX=benheim' "$test_root/r2.render" || fail "R2 artifact preserves the existing default prefix"
[[ $(stat -f '%Lp' "$test_root/r2.render" 2>/dev/null || stat -c '%a' "$test_root/r2.render") == 600 ]] || fail "R2 artifact is mode 0600"
pass "R2 artifact receives process credentials and preserves the backup prefix"
assert_not_contains "R2 rendering does not print credentials" r2-access-sentinel "$test_root/r2.render.out"
assert_not_contains "R2 rendering does not print the secret key" r2-secret-sentinel "$test_root/r2.render.out"

mkdir -p "$test_root/fake-bin"
cat > "$test_root/fake-bin/ssh" <<'EOF'
#!/usr/bin/env bash
printf 'ssh\n' >> "$REMOTE_LOG"
exit 99
EOF
cat > "$test_root/fake-bin/scp" <<'EOF'
#!/usr/bin/env bash
printf 'scp\n' >> "$REMOTE_LOG"
exit 99
EOF
chmod 0755 "$test_root/fake-bin/ssh" "$test_root/fake-bin/scp"
: > "$test_root/remote.log"
set +e
env -u VALHEIM_R2_ACCESS_KEY_ID -u VALHEIM_R2_SECRET_ACCESS_KEY \
  REMOTE_LOG="$test_root/remote.log" \
  PATH="$test_root/fake-bin:$PATH" \
  VALHEIM_ENV_FILE="$test_root/r2.env" \
  VALHEIM_PASSWORD=server-secret-sentinel \
  "$repo_root/scripts/install-server.sh" > "$test_root/install-missing-r2.out" 2>&1
install_status=$?
set -e
if (( install_status == 0 )); then
  fail "R2 install rejects missing process credentials"
fi
[[ ! -s "$test_root/remote.log" ]] || fail "R2 preflight fails before remote calls"
pass "R2 preflight fails before remote calls"
assert_not_contains "R2 preflight does not print process secrets" server-secret-sentinel "$test_root/install-missing-r2.out"

cat > "$test_root/fake-bin/ssh" <<'EOF'
#!/usr/bin/env bash
printf 'ssh <%s>\n' "$*" >> "$REMOTE_LOG"
exit 0
EOF
cat > "$test_root/fake-bin/scp" <<'EOF'
#!/usr/bin/env bash
printf 'scp <%s>\n' "$*" >> "$REMOTE_LOG"
exit 99
EOF
chmod 0755 "$test_root/fake-bin/ssh" "$test_root/fake-bin/scp"

: > "$test_root/remote.log"
if REMOTE_LOG="$test_root/remote.log" \
  PATH="$test_root/fake-bin:$PATH" \
  VALHEIM_ENV_FILE="$test_root/base.env" \
  VALHEIM_PASSWORD=transfer-secret-sentinel \
  "$repo_root/scripts/install-server.sh" > "$test_root/install-transfer.out" 2>&1; then
  fail "install reports a failed transfer"
fi
assert_contains "install restricts its remote staging directory" \
  "install -d -m 0700 /tmp/valheim-server" "$test_root/remote.log"
assert_contains "install cleans staging after a failed transfer" \
  "rm -rf /tmp/valheim-server" "$test_root/remote.log"
assert_not_contains "failed install transfer does not print the password" \
  transfer-secret-sentinel "$test_root/install-transfer.out" "$test_root/remote.log"

: > "$test_root/remote.log"
if REMOTE_LOG="$test_root/remote.log" \
  PATH="$test_root/fake-bin:$PATH" \
  VALHEIM_ENV_FILE="$test_root/base.env" \
  VALHEIM_PASSWORD=transfer-secret-sentinel \
  "$repo_root/scripts/apply-server-config.sh" > "$test_root/config-transfer.out" 2>&1; then
  fail "config deployment reports a failed transfer"
fi
assert_contains "config deployment restricts its remote staging directory" \
  "install -d -m 0700 /tmp/valheim-server-config" "$test_root/remote.log"
assert_contains "config deployment cleans staging after a failed transfer" \
  "rm -rf /tmp/valheim-server-config" "$test_root/remote.log"
assert_not_contains "failed config transfer does not print the password" \
  transfer-secret-sentinel "$test_root/config-transfer.out" "$test_root/remote.log"

: > "$test_root/remote.log"
if env -u VALHEIM_PASSWORD \
  REMOTE_LOG="$test_root/remote.log" \
  PATH="$test_root/fake-bin:$PATH" \
  VALHEIM_ENV_FILE="$test_root/base.env" \
  "$repo_root/scripts/install-server.sh" > "$test_root/install-missing-password.out" 2>&1; then
  fail "install rejects a missing process password"
fi
[[ ! -s "$test_root/remote.log" ]] || fail "install password preflight fails before remote calls"
pass "install password preflight fails before remote calls"

: > "$test_root/remote.log"
if env -u VALHEIM_PASSWORD \
  REMOTE_LOG="$test_root/remote.log" \
  PATH="$test_root/fake-bin:$PATH" \
  VALHEIM_ENV_FILE="$test_root/base.env" \
  "$repo_root/scripts/apply-server-config.sh" > "$test_root/config-missing-password.out" 2>&1; then
  fail "config deployment rejects a missing process password"
fi
[[ ! -s "$test_root/remote.log" ]] || fail "config password preflight fails before remote calls"
pass "config password preflight fails before remote calls"

assert_contains "config rollback snapshots the readiness helper" \
  'old_waiter="$work/wait-for-valheim.previous"' "$repo_root/scripts/apply-server-config.sh"
assert_contains "config rollback restores the readiness helper" \
  'install -m 0755 "$old_waiter" /usr/local/bin/valheim-wait-ready' "$repo_root/scripts/apply-server-config.sh"
assert_contains "R2 runtime credentials are root-only" \
  'install -m 0600 -o root -g root "$work/r2.env" /etc/valheim/r2.env' "$repo_root/scripts/install-server.sh"
assert_not_contains "ordinary installs preserve an existing R2 runtime file" \
  'rm -f /etc/valheim/r2.env' "$repo_root/scripts/install-server.sh"

echo "1..$checks"
