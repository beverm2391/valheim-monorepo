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
  "$game_dir/BepInEx/plugins/BenheimQoL" \
  "$game_dir/BepInEx/plugins/MassFarming" \
  "$fixture/BepInEx/core"
touch "$game_dir/valheim.app/Contents/Resources/PlayerIcon.icns"
touch "$game_dir/BepInEx/plugins/MassFarming/MassFarming.dll"
printf '0.1.33\n' > "$game_dir/BepInEx/plugins/BenheimQoL/VERSION"
printf '#!/bin/sh\n' > "$fixture/start_game_bepinex.sh"
chmod +x "$fixture/start_game_bepinex.sh"

fixture_zip="$test_root/BepInExPack.zip"
(
  cd "$test_root"
  zip -qr "$fixture_zip" BepInExPack_Valheim
)
fixture_sha="$(shasum -a 256 "$fixture_zip" | awk '{print $1}')"
printf 'test-dll\n' > "$test_root/BenheimQoL.dll"
printf '0.1.34\n' > "$test_root/VERSION"

legacy_app="$app_dir/Benheim QoL.app"
managed_updater="$app_dir/Update Benheim.app"
mkdir -p "$legacy_app/Contents" "$managed_updater/Contents"
cat > "$legacy_app/Contents/Info.plist" <<'PLIST'
<plist><dict><key>CFBundleIdentifier</key><string>com.beneverman.benheim-qol</string></dict></plist>
PLIST
cat > "$managed_updater/Contents/Info.plist" <<'PLIST'
<plist><dict><key>CFBundleIdentifier</key><string>com.beneverman.benheim-updater</string></dict></plist>
PLIST

run_installer() {
  BENHEIM_QOL_GAME_DIR="$game_dir" \
  BENHEIM_QOL_APP_DIR="$1" \
  BENHEIM_QOL_DLL="${2:-$test_root/BenheimQoL.dll}" \
  BENHEIM_QOL_VERSION_FILE="$test_root/VERSION" \
  BENHEIM_QOL_LAUNCHER_SOURCE="$root/scripts/macos-launcher.sh" \
  BENHEIM_QOL_BEPINEX_URL="file://$fixture_zip" \
  BENHEIM_QOL_BEPINEX_SHA256="$fixture_sha" \
  BENHEIM_QOL_NONINTERACTIVE=1 \
    "$root/scripts/install-macos.command"
}

run_installer "$app_dir" >/dev/null

test -x "$game_dir/start_game_bepinex.sh"
grep -Fqx 'test-dll' "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll"
grep -Fqx '0.1.34' "$game_dir/BepInEx/plugins/BenheimQoL/VERSION"
test ! -f "$game_dir/BepInEx/plugins/MassFarming/MassFarming.dll"
test -f "$game_dir/BepInEx/disabled/MassFarming/MassFarming.dll"
test ! -e "$legacy_app"
test ! -e "$managed_updater"
test -x "$app_dir/Benheim.app/Contents/MacOS/BenheimQoL"
test -f "$app_dir/Benheim.app/Contents/Resources/PlayerIcon.icns"
grep -Fq 'open -a Steam' "$app_dir/Benheim.app/Contents/MacOS/BenheimQoL"
grep -Fq 'steam_logged_on' "$app_dir/Benheim.app/Contents/MacOS/BenheimQoL"
grep -Fq 'processing complete' "$app_dir/Benheim.app/Contents/MacOS/BenheimQoL"
! grep -Fq 'pgrep -x ipcserver' "$app_dir/Benheim.app/Contents/MacOS/BenheimQoL"

first_plugin_sha="$(shasum -a 256 "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll" | awk '{print $1}')"
first_launcher_sha="$(shasum -a 256 "$app_dir/Benheim.app/Contents/MacOS/BenheimQoL" | awk '{print $1}')"

# A second install converges on the same active plugin and launcher.
run_installer "$app_dir" >/dev/null
test "$first_plugin_sha" = "$(shasum -a 256 "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll" | awk '{print $1}')"
test "$first_launcher_sha" = "$(shasum -a 256 "$app_dir/Benheim.app/Contents/MacOS/BenheimQoL" | awk '{print $1}')"

# A failure after plugin replacement restores both the prior DLL and version.
printf 'new-test-dll\n' > "$test_root/NewBenheimQoL.dll"
blocked_app_parent="$test_root/not-a-directory"
printf 'block launcher directory creation\n' > "$blocked_app_parent"
if run_installer "$blocked_app_parent" "$test_root/NewBenheimQoL.dll" >/dev/null 2>&1; then
  echo "installer succeeded without being able to install the launcher" >&2
  exit 1
fi
test "$first_plugin_sha" = "$(shasum -a 256 "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll" | awk '{print $1}')"
grep -Fqx '0.1.34' "$game_dir/BepInEx/plugins/BenheimQoL/VERSION"

# Never overwrite an unrelated app that uses the launcher target name.
foreign_app_dir="$test_root/Foreign Applications"
mkdir -p "$foreign_app_dir/Benheim.app/Contents"
printf 'not our app\n' > "$foreign_app_dir/Benheim.app/Contents/Info.plist"
if run_installer "$foreign_app_dir" >/dev/null 2>&1; then
  echo "installer replaced an unrelated app" >&2
  exit 1
fi
grep -Fq 'not our app' "$foreign_app_dir/Benheim.app/Contents/Info.plist"

# A retired updater path is removed only when its managed bundle identifier
# proves ownership. A foreign path is left untouched while installation repairs
# the launcher beside it.
foreign_updater_dir="$test_root/Foreign Updater Applications"
mkdir -p "$foreign_updater_dir/Update Benheim.app/Contents"
printf 'not our updater\n' > "$foreign_updater_dir/Update Benheim.app/Contents/Info.plist"
run_installer "$foreign_updater_dir" >/dev/null
grep -Fq 'not our updater' "$foreign_updater_dir/Update Benheim.app/Contents/Info.plist"
test -x "$foreign_updater_dir/Benheim.app/Contents/MacOS/BenheimQoL"

grep -Fq 'Rosetta 2 is required' "$root/scripts/install-macos.command"
test ! -e "$root/scripts/update-macos.sh"
test ! -e "$root/tests/macos-updater-test.sh"
! grep -Fq 'BENHEIM_UPDATE' "$root/scripts/install-macos.command"
! grep -Fq 'update-macos.sh' "$root/scripts/package-macos.sh"

# The shareable package contains exactly the installer, launcher, version, and DLL.
BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
BENHEIM_QOL_DIST="$test_root/dist" \
BENHEIM_QOL_SKIP_BUILD=1 \
  "$root/scripts/package-macos.sh" >/dev/null
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
package="$test_root/dist/Benheim-macOS-$version.zip"
package_entries="$(unzip -Z1 "$package" | sort)"
expected_entries="$(printf '%s\n' \
  "Benheim-macOS-$version/" \
  "Benheim-macOS-$version/BenheimQoL.dll" \
  "Benheim-macOS-$version/Install Benheim.command" \
  "Benheim-macOS-$version/VERSION" \
  "Benheim-macOS-$version/macos-launcher.sh" | sort)"
test "$package_entries" = "$expected_entries"
grep -Fq 'BENHEIM_QOL_VERSION_FILE="$version_file"' "$root/scripts/install-local.sh"

echo "macOS installer, migration, and package checks passed"
