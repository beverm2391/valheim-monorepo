#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

game_dir="$test_root/Valheim Profile"
dll="$test_root/ValheimDev.dll"
mkdir -p "$game_dir/BepInEx/plugins"
printf 'standalone plugin\n' > "$dll"

VALHEIM_GAME_DIR="$game_dir" \
VALHEIM_DEV_DLL="$dll" \
VALHEIM_DEV_SKIP_BUILD=1 \
  "$root/scripts/install-local.sh" >/dev/null

cmp -s "$dll" "$game_dir/BepInEx/plugins/ValheimDev/ValheimDev.dll"
test -f "$game_dir/BepInEx/ValheimDev/registry/give-item/code.cs"
printf 'local edit\n' > "$game_dir/BepInEx/ValheimDev/registry/give-item/code.cs"

VALHEIM_GAME_DIR="$game_dir" \
VALHEIM_DEV_DLL="$dll" \
VALHEIM_DEV_SKIP_BUILD=1 \
  "$root/scripts/install-local.sh" >/dev/null

grep -Fxq 'local edit' "$game_dir/BepInEx/ValheimDev/registry/give-item/code.cs"

if env -u VALHEIM_GAME_DIR -u VALHEIM_DEV_ROOT \
  VALHEIM_DEV_DLL="$dll" VALHEIM_DEV_SKIP_BUILD=1 \
  "$root/scripts/install-local.sh" >"$test_root/missing.out" 2>&1; then
  echo "installer accepted an implicit Valheim profile" >&2
  exit 1
fi
grep -Fq 'VALHEIM_GAME_DIR must name the active Valheim profile' "$test_root/missing.out"

echo "Valheim Dev installer keeps profile, plugin, and runtime-data ownership explicit"
