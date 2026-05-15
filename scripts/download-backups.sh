#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

root="$(repo_root)"
mkdir -p "$root/backups"

target="$(ssh_target)"
if [[ -n "${SSH_KEY_PATH:-}" ]]; then
  rsync -av -e "ssh -i $SSH_KEY_PATH -o StrictHostKeyChecking=accept-new" "$target:/var/backups/valheim/" "$root/backups/"
else
  rsync -av -e "ssh -o StrictHostKeyChecking=accept-new" "$target:/var/backups/valheim/" "$root/backups/"
fi

