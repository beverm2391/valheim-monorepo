#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

if ! diagnostics_config_requested; then
  echo "Set VALHEIM_DIAGNOSTICS_CONFIGURE=1 before applying diagnostics." >&2
  exit 1
fi

root="$(repo_root)"
tmp_diagnostics="$(mktemp)"
remote_stage_created=0
cleanup() {
  local status=$?
  rm -f "$tmp_diagnostics"
  if (( remote_stage_created == 1 )); then
    remote_ssh "rm -rf /tmp/valheim-diagnostics" >/dev/null 2>&1 || true
  fi
  return "$status"
}
trap cleanup EXIT

render_diagnostics_env "$tmp_diagnostics"

remote_ssh "install -d -m 0700 /tmp/valheim-diagnostics"
remote_stage_created=1
remote_scp "$root/systemd/valheim-diagnostics.service" "/tmp/valheim-diagnostics/valheim-diagnostics.service"
remote_scp "$root/server/forward-valheim-failures.py" "/tmp/valheim-diagnostics/forward-valheim-failures.py"
remote_scp "$root/server/valheim_failure_events.py" "/tmp/valheim-diagnostics/valheim_failure_events.py"
remote_scp "$tmp_diagnostics" "/tmp/valheim-diagnostics/diagnostics.env"

remote_ssh 'bash -s' <<'REMOTE'
set -euo pipefail

work=/tmp/valheim-diagnostics
cleanup_stage() {
  rm -rf "$work"
}
trap cleanup_stage EXIT

install -d -m 0755 /etc/valheim
install -m 0600 -o root -g root "$work/diagnostics.env" /etc/valheim/diagnostics.env
install -m 0644 "$work/valheim-diagnostics.service" /etc/systemd/system/valheim-diagnostics.service
install -m 0755 "$work/forward-valheim-failures.py" /usr/local/bin/valheim-forward-failures
install -m 0644 "$work/valheim_failure_events.py" /usr/local/bin/valheim_failure_events.py
systemctl daemon-reload
systemctl enable valheim-diagnostics.service
systemctl restart valheim-diagnostics.service
systemctl is-active --quiet valheim-diagnostics.service
REMOTE
remote_stage_created=0

echo "Applied Valheim diagnostics on $(ssh_target) without restarting valheim.service"
