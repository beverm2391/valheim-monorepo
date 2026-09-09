#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

root="$(repo_root)"
remote_stage_created=0
cleanup() {
  local status=$?
  if (( remote_stage_created == 1 )); then
    remote_ssh "rm -rf /tmp/valheim-server-archive" >/dev/null 2>&1 || true
  fi
  return "$status"
}
trap cleanup EXIT
remote_ssh "install -d -m 0700 /tmp/valheim-server-archive"
remote_stage_created=1
remote_scp "$root/server/archive-valheim-installation" "/tmp/valheim-server-archive/archive-valheim-installation"
remote_ssh 'bash /tmp/valheim-server-archive/archive-valheim-installation; status=$?; rm -rf /tmp/valheim-server-archive; exit $status'
remote_stage_created=0
