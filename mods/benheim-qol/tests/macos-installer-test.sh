#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

game_dir="$test_root/Valheim"
app_dir="$test_root/Applications"
fixture="$test_root/BepInExPack_Valheim"
mkdir -p \
  "$game_dir/valheim.app/Contents/Resources" \
  "$game_dir/BepInEx/plugins/MassFarming" \
  "$fixture/BepInEx/core"
touch "$game_dir/valheim.app/Contents/Resources/PlayerIcon.icns"
touch "$game_dir/BepInEx/plugins/MassFarming/MassFarming.dll"
printf '#!/bin/sh\n' > "$fixture/start_game_bepinex.sh"
chmod +x "$fixture/start_game_bepinex.sh"

fixture_zip="$test_root/BepInExPack.zip"
(
  cd "$test_root"
  zip -qr "$fixture_zip" BepInExPack_Valheim
)
fixture_sha="$(shasum -a 256 "$fixture_zip" | awk '{print $1}')"

printf 'test-dll\n' > "$test_root/BenheimQoL.dll"
BENHEIM_QOL_GAME_DIR="$game_dir" \
BENHEIM_QOL_APP_DIR="$app_dir" \
BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
BENHEIM_QOL_LAUNCHER_SOURCE="$root/scripts/macos-launcher.sh" \
BENHEIM_QOL_BEPINEX_URL="file://$fixture_zip" \
BENHEIM_QOL_BEPINEX_SHA256="$fixture_sha" \
BENHEIM_QOL_NONINTERACTIVE=1 \
  "$root/scripts/install-macos.command" >/dev/null

test -x "$game_dir/start_game_bepinex.sh"
test -f "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll"
test ! -f "$game_dir/BepInEx/plugins/MassFarming/MassFarming.dll"
test -f "$game_dir/BepInEx/disabled/MassFarming/MassFarming.dll"
test -x "$app_dir/Benheim QoL.app/Contents/MacOS/BenheimQoL"
test -f "$app_dir/Benheim QoL.app/Contents/Resources/PlayerIcon.icns"
grep -Fq 'open -a Steam' "$app_dir/Benheim QoL.app/Contents/MacOS/BenheimQoL"
grep -Fq 'pgrep -x ipcserver' "$app_dir/Benheim QoL.app/Contents/MacOS/BenheimQoL"
grep -Fq 'Rosetta 2 is required' "$root/scripts/install-macos.command"

first_plugin_sha="$(shasum -a 256 "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll" | awk '{print $1}')"
first_launcher_sha="$(shasum -a 256 "$app_dir/Benheim QoL.app/Contents/MacOS/BenheimQoL" | awk '{print $1}')"

# A second install must converge on the same active plugin and launcher.
BENHEIM_QOL_GAME_DIR="$game_dir" \
BENHEIM_QOL_APP_DIR="$app_dir" \
BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
BENHEIM_QOL_LAUNCHER_SOURCE="$root/scripts/macos-launcher.sh" \
BENHEIM_QOL_BEPINEX_URL="file://$fixture_zip" \
BENHEIM_QOL_BEPINEX_SHA256="$fixture_sha" \
BENHEIM_QOL_NONINTERACTIVE=1 \
  "$root/scripts/install-macos.command" >/dev/null

test "$first_plugin_sha" = "$(shasum -a 256 "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll" | awk '{print $1}')"
test "$first_launcher_sha" = "$(shasum -a 256 "$app_dir/Benheim QoL.app/Contents/MacOS/BenheimQoL" | awk '{print $1}')"

# Never overwrite an unrelated app that happens to use the target name.
foreign_app_dir="$test_root/Foreign Applications"
mkdir -p "$foreign_app_dir/Benheim QoL.app/Contents"
printf 'not our app\n' > "$foreign_app_dir/Benheim QoL.app/Contents/Info.plist"
if BENHEIM_QOL_GAME_DIR="$game_dir" \
  BENHEIM_QOL_APP_DIR="$foreign_app_dir" \
  BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
  BENHEIM_QOL_LAUNCHER_SOURCE="$root/scripts/macos-launcher.sh" \
  BENHEIM_QOL_BEPINEX_URL="file://$fixture_zip" \
  BENHEIM_QOL_BEPINEX_SHA256="$fixture_sha" \
  BENHEIM_QOL_NONINTERACTIVE=1 \
    "$root/scripts/install-macos.command" >/dev/null 2>&1; then
  echo "installer replaced an unrelated app" >&2
  exit 1
fi
grep -Fq 'not our app' "$foreign_app_dir/Benheim QoL.app/Contents/Info.plist"

echo "macOS installer checks passed"
