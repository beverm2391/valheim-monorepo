#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

keep_stopped=0
case "${1:-}" in
  "") ;;
  --keep-stopped) keep_stopped=1 ;;
  *)
    echo "Usage: $0 [--keep-stopped]" >&2
    exit 1
    ;;
esac

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
remote_scp "$root/server/apply-valheim-config" "/tmp/valheim-server-config/apply-valheim-config"
remote_scp "$tmp_env" "/tmp/valheim-server-config/server.env"

remote_ssh "bash /tmp/valheim-server-config/apply-valheim-config '$keep_stopped'"
remote_stage_created=0
