#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

root="$(repo_root)"
tmp_env="$(mktemp)"
remote_stage_created=0
cleanup_local() {
  local status=$?
  rm -f "$tmp_env"
  if (( remote_stage_created == 1 )); then
    remote_ssh "rm -rf /tmp/valheim-server-config" >/dev/null 2>&1 || true
  fi
  return "$status"
}
trap cleanup_local EXIT
render_server_env "$tmp_env"

remote_ssh "install -d -m 0700 /tmp/valheim-server-config"
remote_stage_created=1
remote_scp "$root/server/valheim-start" "/tmp/valheim-server-config/valheim-start"
remote_scp "$root/server/wait-for-valheim" "/tmp/valheim-server-config/wait-for-valheim"
remote_scp "$tmp_env" "/tmp/valheim-server-config/server.env"

remote_ssh 'bash -s' <<'REMOTE'
set -euo pipefail

work=/tmp/valheim-server-config
cleanup_stage() {
  rm -rf "$work"
}
old_env="$work/server.env.previous"
old_launcher="$work/valheim-start.previous"
old_waiter="$work/wait-for-valheim.previous"
cp /etc/valheim/server.env "$old_env"
cp /usr/local/bin/valheim-start "$old_launcher"
cp /usr/local/bin/valheim-wait-ready "$old_waiter"

recover_previous() {
  local status=$?
  if [[ $status -ne 0 ]]; then
    echo "Config deployment failed; restoring the previous launcher and environment." >&2
    systemctl stop valheim.service || true
    install -m 0640 -o root -g valheim "$old_env" /etc/valheim/server.env
    install -m 0755 "$old_launcher" /usr/local/bin/valheim-start
    install -m 0755 "$old_waiter" /usr/local/bin/valheim-wait-ready
    systemctl start valheim.service || true
  fi
  cleanup_stage
  exit "$status"
}
trap recover_previous EXIT

systemctl stop valheim.service
valheim-backup-and-upload
install -m 0640 -o root -g valheim "$work/server.env" /etc/valheim/server.env
install -m 0755 "$work/valheim-start" /usr/local/bin/valheim-start
install -m 0755 "$work/wait-for-valheim" /usr/local/bin/valheim-wait-ready
started_at="$(date --iso-8601=seconds)"
systemctl start valheim.service
valheim-wait-ready "$started_at"

trap - EXIT
cleanup_stage
echo "Applied server configuration."
REMOTE
remote_stage_created=0
