#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
mod_root="$root/server-mods/benheim-test-commands"
game_dir="${VALHEIM_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
managed_dir="${VALHEIM_MANAGED_DIR:-$game_dir/valheim.app/Contents/Resources/Data/Managed}"
bepinex_core_dir="${BEPINEX_CORE_DIR:-$game_dir/BepInEx/core}"

for dependency in \
  "$bepinex_core_dir/BepInEx.dll" \
  "$bepinex_core_dir/0Harmony.dll" \
  "$managed_dir/assembly_valheim.dll" \
  "$managed_dir/UnityEngine.CoreModule.dll" \
  "$managed_dir/UnityEngine.PhysicsModule.dll"; do
  if [[ ! -f "$dependency" ]]; then
    printf 'Missing build dependency: %s\n' "$dependency" >&2
    exit 1
  fi
done

dotnet build "$mod_root/src/BenheimTestCommands.csproj" \
  --configuration Release \
  -p:ValheimManagedDir="$managed_dir" \
  -p:BepInExCoreDir="$bepinex_core_dir"
