#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

echo "Server: $HETZNER_SERVER_NAME ($(server_ip))"
remote_ssh '
  set -euo pipefail
  echo "valheim.service: $(systemctl is-active valheim.service)"
  echo "valheim.service enabled: $(systemctl is-enabled valheim.service)"
  echo
  echo "Recent server readiness logs:"
  journalctl -u valheim.service -n 160 --no-pager \
    | grep -E "Load world|Loading [0-9]+ zdos|Opened Steam server|Game server connected|Connections [0-9]+" \
    | tail -n 12 || true
  echo
  echo "UDP listeners:"
  ss -lunp | grep -E "2456|2457|2458" || true
'
