#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_dir="${VALHEIM_GAME_DIR:-}"
dll="${VALHEIM_DEV_DLL:-$root/plugin/bin/Release/netstandard2.1/ValheimDev.dll}"

if [[ -z "$game_dir" ]]; then
  echo "VALHEIM_GAME_DIR must name the active Valheim profile." >&2
  exit 1
fi
if pgrep -x valheim >/dev/null 2>&1 \
  || pgrep -x valheim.x86_64 >/dev/null 2>&1 \
  || pgrep -f "$game_dir/valheim.app/Contents/MacOS" >/dev/null 2>&1; then
  echo "Valheim is running. Quit it before installing Valheim Dev." >&2
  exit 1
fi
if [[ "${VALHEIM_DEV_SKIP_BUILD:-0}" != "1" ]]; then
  "$root/scripts/build.sh"
fi
if [[ ! -f "$dll" ]]; then
  echo "Valheim Dev build output is missing: $dll" >&2
  exit 1
fi
if [[ ! -d "$game_dir/BepInEx/plugins" ]]; then
  echo "The active BepInEx profile is missing: $game_dir/BepInEx/plugins" >&2
  exit 1
fi

plugin_dir="$game_dir/BepInEx/plugins/ValheimDev"
data_root="${VALHEIM_DEV_ROOT:-$game_dir/BepInEx/ValheimDev}"
install -d "$plugin_dir" "$data_root/registry"
plugin_tmp="$plugin_dir/.ValheimDev.dll.$$"
install -m 0644 "$dll" "$plugin_tmp"
mv -f "$plugin_tmp" "$plugin_dir/ValheimDev.dll"

for template in "$root"/recipe-templates/*; do
  [[ -d "$template" ]] || continue
  recipe="$data_root/registry/$(basename "$template")"
  if [[ ! -e "$recipe" ]]; then
    cp -R "$template" "$recipe"
  fi
done

echo "Installed Valheim Dev in $plugin_dir"
echo "Runtime data root: $data_root"
