#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
game_dir="${VALHEIM_GAME_DIR:-}"

if [[ -z "$game_dir" ]]; then
  echo "VALHEIM_GAME_DIR must name the active Valheim profile." >&2
  exit 1
fi

dotnet build "$root/plugin/ValheimDev.csproj" \
  --configuration Release \
  -p:ValheimGameDir="$game_dir"
