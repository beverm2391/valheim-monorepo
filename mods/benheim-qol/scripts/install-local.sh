#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_dir="${VALHEIM_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
plugin_dir="$game_dir/BepInEx/plugins/BenheimQoL"
dll="$root/src/bin/Release/netstandard2.1/BenheimQoL.dll"

"$root/scripts/build.sh"

if [[ ! -f "$dll" ]]; then
  echo "Missing build output: $dll" >&2
  exit 1
fi

install -d "$plugin_dir"
install -m 0644 "$dll" "$plugin_dir/BenheimQoL.dll"

echo "Installed $plugin_dir/BenheimQoL.dll"
