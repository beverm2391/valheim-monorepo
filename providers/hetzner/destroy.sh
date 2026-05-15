#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib.sh"
load_config

echo "This will delete Hetzner server '$HETZNER_SERVER_NAME'."
read -r -p "Type the server name to confirm: " confirm
if [[ "$confirm" != "$HETZNER_SERVER_NAME" ]]; then
  echo "Cancelled."
  exit 1
fi

hcloud_cmd server delete "$HETZNER_SERVER_NAME"

