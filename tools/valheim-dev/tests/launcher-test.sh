#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT
mkdir -p "$test_root/bin" "$test_root/runtime root"

printf '%s\n' '#!/usr/bin/env bash' \
  'printf "%s|%s\n" "$VALHEIM_DEV_ROOT" "$1"' > "$test_root/bin/node"
chmod +x "$test_root/bin/node"

actual="$(PATH="$test_root/bin:$PATH" VALHEIM_DEV_ROOT="$test_root/runtime root" "$root/launch.sh")"
test "$actual" = "$test_root/runtime root|$root/server.mjs"

game_dir="$test_root/active profile"
actual="$(PATH="$test_root/bin:$PATH" VALHEIM_GAME_DIR="$game_dir" "$root/launch.sh")"
test "$actual" = "$game_dir/BepInEx/ValheimDev|$root/server.mjs"

if env -u VALHEIM_DEV_ROOT -u VALHEIM_GAME_DIR PATH="$test_root/bin:$PATH" \
  "$root/launch.sh" >"$test_root/missing.out" 2>&1; then
  echo "launcher accepted an unresolved runtime root" >&2
  exit 1
fi
grep -Fq 'Valheim Dev runtime root is unresolved' "$test_root/missing.out"

echo "Valheim Dev launcher owns its source path and requires an explicit runtime root"
