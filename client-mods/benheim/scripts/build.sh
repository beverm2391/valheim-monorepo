#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_dir="${VALHEIM_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"

dotnet build "$root/src/BenheimQoL.csproj" \
  -c Release \
  -p:ValheimGameDir="$game_dir"
