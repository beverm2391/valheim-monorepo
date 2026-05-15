#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 path/to/world.db path/to/world.fwl" >&2
  exit 1
fi

db_file=$1
fwl_file=$2

if [[ ! -f "$db_file" || ! -f "$fwl_file" ]]; then
  echo "Both .db and .fwl files must exist." >&2
  exit 1
fi

db_base="$(basename "$db_file")"
fwl_base="$(basename "$fwl_file")"
expected_db="${VALHEIM_WORLD_NAME}.db"
expected_fwl="${VALHEIM_WORLD_NAME}.fwl"

if [[ "$db_base" != "$expected_db" || "$fwl_base" != "$expected_fwl" ]]; then
  echo "World files must match VALHEIM_WORLD_NAME=$VALHEIM_WORLD_NAME:" >&2
  echo "  expected $expected_db and $expected_fwl" >&2
  exit 1
fi

remote_ssh "systemctl stop valheim.service >/dev/null 2>&1 || true; install -d -o valheim -g valheim /var/lib/valheim/worlds_local"
remote_scp "$db_file" "/tmp/$expected_db"
remote_scp "$fwl_file" "/tmp/$expected_fwl"
remote_ssh "install -o valheim -g valheim -m 0644 /tmp/$expected_db /var/lib/valheim/worlds_local/$expected_db && install -o valheim -g valheim -m 0644 /tmp/$expected_fwl /var/lib/valheim/worlds_local/$expected_fwl && rm -f /tmp/$expected_db /tmp/$expected_fwl && systemctl start valheim.service"

echo "Uploaded world '$VALHEIM_WORLD_NAME' and started Valheim."
