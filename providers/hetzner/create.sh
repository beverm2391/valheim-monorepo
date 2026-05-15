#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../../scripts/lib.sh"
load_config

: "${HETZNER_LOCATION:=ash}"
: "${HETZNER_SERVER_TYPE:=cpx21}"
: "${HETZNER_IMAGE:=ubuntu-24.04}"
: "${HETZNER_SSH_KEY:?Set HETZNER_SSH_KEY to an existing Hetzner SSH key name or ID}"

if hcloud_cmd server describe "$HETZNER_SERVER_NAME" >/dev/null 2>&1; then
  echo "Server already exists: $HETZNER_SERVER_NAME"
else
  hcloud_cmd server create \
    --name "$HETZNER_SERVER_NAME" \
    --type "$HETZNER_SERVER_TYPE" \
    --image "$HETZNER_IMAGE" \
    --location "$HETZNER_LOCATION" \
    --ssh-key "$HETZNER_SSH_KEY" \
    --label app=valheim-server \
    --label role=game
fi

firewall_name="${HETZNER_SERVER_NAME}-valheim"
if ! hcloud_cmd firewall describe "$firewall_name" >/dev/null 2>&1; then
  hcloud_cmd firewall create --name "$firewall_name"
  hcloud_cmd firewall add-rule "$firewall_name" --direction in --protocol tcp --port 22 --source-ips 0.0.0.0/0 --source-ips ::/0
  hcloud_cmd firewall add-rule "$firewall_name" --direction in --protocol udp --port "${VALHEIM_PORT}-$((VALHEIM_PORT + 2))" --source-ips 0.0.0.0/0 --source-ips ::/0
fi

hcloud_cmd firewall apply-to-resource "$firewall_name" --type server --server "$HETZNER_SERVER_NAME"

echo "Server IP: $(server_ip)"

