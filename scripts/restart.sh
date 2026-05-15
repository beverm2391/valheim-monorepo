#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

remote_ssh "systemctl restart valheim.service && systemctl --no-pager --full status valheim.service"

