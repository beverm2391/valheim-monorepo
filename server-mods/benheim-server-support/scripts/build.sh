#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
mod_root="$root/server-mods/benheim-server-support"
game_dir="${VALHEIM_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
managed_dir="${VALHEIM_MANAGED_DIR:-$game_dir/valheim.app/Contents/Resources/Data/Managed}"
bepinex_core_dir="${BEPINEX_CORE_DIR:-$game_dir/BepInEx/core}"

for file in \
  "$managed_dir/assembly_valheim.dll" \
  "$managed_dir/assembly_utils.dll" \
  "$managed_dir/UnityEngine.dll" \
  "$managed_dir/UnityEngine.CoreModule.dll" \
  "$bepinex_core_dir/BepInEx.dll" \
  "$bepinex_core_dir/0Harmony.dll"; do
  if [[ ! -f "$file" ]]; then
    echo "Missing build reference: $file" >&2
    exit 1
  fi
done

dotnet build "$mod_root/src/BenheimServerSupport.csproj" \
  --configuration Release \
  -p:ValheimManagedDir="$managed_dir" \
  -p:BepInExCoreDir="$bepinex_core_dir"

mkdir -p "$mod_root/dist"
install -m 0644 \
  "$mod_root/src/bin/Release/netstandard2.1/BenheimServerSupport.dll" \
  "$mod_root/dist/BenheimServerSupport.dll"

shasum -a 256 "$mod_root/dist/BenheimServerSupport.dll"
