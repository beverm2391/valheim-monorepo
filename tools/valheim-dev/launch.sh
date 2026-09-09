#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [[ -n "${VALHEIM_DEV_ROOT:-}" ]]; then
  export VALHEIM_DEV_ROOT
elif [[ -n "${VALHEIM_GAME_DIR:-}" ]]; then
  export VALHEIM_DEV_ROOT="$VALHEIM_GAME_DIR/BepInEx/ValheimDev"
else
  echo "Valheim Dev runtime root is unresolved. Set VALHEIM_DEV_ROOT or VALHEIM_GAME_DIR." >&2
  exit 1
fi

exec node "$script_dir/server.mjs"
