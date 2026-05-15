#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

echo "Server: $HETZNER_SERVER_NAME ($(server_ip))"
remote_ssh "systemctl --no-pager --full status valheim.service || true"

