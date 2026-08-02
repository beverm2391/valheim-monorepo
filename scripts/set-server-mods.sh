#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

case "${1:-}" in
  enable) value=1 ;;
  disable) value=0 ;;
  *)
    echo "Usage: $0 enable|disable" >&2
    exit 1
    ;;
esac

remote_ssh "bash -s -- '$value'" <<'REMOTE'
set -euo pipefail
value=$1

if [[ "$value" == "1" && ! -f /opt/valheim/server/BepInEx/core/BepInEx.Preloader.dll ]]; then
  echo "Cannot enable server mods: BepInEx is not installed." >&2
  exit 1
fi

previous_value=0
if grep -q '^VALHEIM_MODDED=' /etc/valheim/server.env; then
  previous_value="$(sed -n 's/^VALHEIM_MODDED=//p' /etc/valheim/server.env)"
fi

set_modded() {
  local new_value=$1
  if grep -q '^VALHEIM_MODDED=' /etc/valheim/server.env; then
    sed -i "s/^VALHEIM_MODDED=.*/VALHEIM_MODDED=$new_value/" /etc/valheim/server.env
  else
    printf '\nVALHEIM_MODDED=%s\n' "$new_value" >> /etc/valheim/server.env
  fi
}

restore_previous() {
  local status=$?
  if [[ $status -ne 0 ]]; then
    echo "Mod toggle failed; restoring VALHEIM_MODDED=$previous_value." >&2
    systemctl stop valheim.service || true
    set_modded "$previous_value"
    systemctl start valheim.service || true
  fi
  exit "$status"
}
trap restore_previous EXIT

set_modded "$value"
started_at="$(date --iso-8601=seconds)"
systemctl restart valheim.service
valheim-wait-ready "$started_at"

trap - EXIT
echo "Set VALHEIM_MODDED=$value and reached Game server connected."
REMOTE
