#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
game_dir="${VALHEIM_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
export VALHEIM_DEV_ROOT="${VALHEIM_DEV_ROOT:-$game_dir/BepInEx/ValheimDev}"

exec node "$script_dir/server.mjs"
