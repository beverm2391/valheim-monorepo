#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

game_dir="$test_root/Valheim"
app_dir="$test_root/Applications"
fixture="$test_root/BepInExPack_Valheim"
mock_bin="$test_root/bin"
mkdir -p \
  "$game_dir/valheim.app/Contents/Resources" \
  "$game_dir/BepInEx/plugins/BenheimQoL" \
  "$game_dir/BepInEx/plugins/MassFarming" \
  "$fixture/BepInEx/core" \
  "$mock_bin"
cat > "$mock_bin/pgrep" <<'EOF'
#!/usr/bin/env bash
exit 1
EOF
chmod +x "$mock_bin/pgrep"
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
  private_diagnostics="${3:-$test_root/no-private-diagnostics.cfg}"
  PATH="$mock_bin:$PATH" \
  BENHEIM_QOL_GAME_DIR="$game_dir" \
  BENHEIM_QOL_APP_DIR="$1" \
  BENHEIM_QOL_DLL="${2:-$test_root/BenheimQoL.dll}" \
  BENHEIM_QOL_VERSION_FILE="$test_root/VERSION" \
  BENHEIM_QOL_LAUNCHER_SOURCE="$root/scripts/macos-launcher.sh" \
  BENHEIM_QOL_PRIVATE_DIAGNOSTICS_FILE="$private_diagnostics" \
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

private_config_source="$test_root/PRIVATE-TEST-DIAGNOSTICS.cfg"
private_config_installed="$game_dir/BepInEx/config/BenheimPrivateDiagnostics.cfg"
printf '%s\n' \
  'BENHEIM_PRIVATE_DIAGNOSTICS_V1' \
  'endpoint=https://us-east-1.aws.edge.axiom.co' \
  'dataset=benheim-diagnostics' \
  'token=first-private-sentinel' \
  'build_id=sha256:first-build' > "$private_config_source"
run_installer "$app_dir" "$test_root/BenheimQoL.dll" "$private_config_source" >/dev/null
cmp -s "$private_config_source" "$private_config_installed"
test "$(stat -f '%Lp' "$private_config_installed")" = 600

# A normal public package intentionally removes private-test credentials.
run_installer "$app_dir" >/dev/null
test ! -e "$private_config_installed"

# A failure after replacing a private config restores the prior credential file.
run_installer "$app_dir" "$test_root/BenheimQoL.dll" "$private_config_source" >/dev/null
private_config_previous_sha="$(shasum -a 256 "$private_config_installed" | awk '{print $1}')"
replacement_private_config="$test_root/Replacement-PRIVATE-TEST-DIAGNOSTICS.cfg"
sed 's/first-private-sentinel/replacement-private-sentinel/' \
  "$private_config_source" > "$replacement_private_config"

# A second install converges on the same active plugin and launcher.
run_installer "$app_dir" "$test_root/BenheimQoL.dll" "$private_config_source" >/dev/null
test "$first_plugin_sha" = "$(shasum -a 256 "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll" | awk '{print $1}')"
test "$first_launcher_sha" = "$(shasum -a 256 "$app_dir/Benheim.app/Contents/MacOS/BenheimQoL" | awk '{print $1}')"

# A failure after plugin replacement restores both the prior DLL and version.
printf 'new-test-dll\n' > "$test_root/NewBenheimQoL.dll"
blocked_app_parent="$test_root/not-a-directory"
printf 'block launcher directory creation\n' > "$blocked_app_parent"
if run_installer \
  "$blocked_app_parent" \
  "$test_root/NewBenheimQoL.dll" \
  "$replacement_private_config" >/dev/null 2>&1; then
  echo "installer succeeded without being able to install the launcher" >&2
  exit 1
fi
test "$first_plugin_sha" = "$(shasum -a 256 "$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll" | awk '{print $1}')"
grep -Fqx '0.1.34' "$game_dir/BepInEx/plugins/BenheimQoL/VERSION"
test "$private_config_previous_sha" = \
  "$(shasum -a 256 "$private_config_installed" | awk '{print $1}')"

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

# The shareable package contains exactly the installer, process check, launcher,
# version, and DLL.
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
  "Benheim-macOS-$version/check-valheim-stopped.sh" \
  "Benheim-macOS-$version/macos-launcher.sh" | sort)"
test "$package_entries" = "$expected_entries"

# The local package workflow validates the archive, invokes its shipped
# installer, and verifies the installed VERSION and DLL against that payload.
packaged_game_dir="$test_root/Packaged Valheim"
packaged_app_dir="$test_root/Packaged Applications"
mkdir -p "$packaged_game_dir/valheim.app/Contents/Resources"
touch "$packaged_game_dir/valheim.app/Contents/Resources/PlayerIcon.icns"
package_output="$(
  PATH="$mock_bin:$PATH" \
  BENHEIM_QOL_GAME_DIR="$packaged_game_dir" \
  BENHEIM_QOL_APP_DIR="$packaged_app_dir" \
  BENHEIM_QOL_DLL="$test_root/NewBenheimQoL.dll" \
  BENHEIM_QOL_BEPINEX_URL="file://$fixture_zip" \
  BENHEIM_QOL_BEPINEX_SHA256="$fixture_sha" \
    "$root/scripts/install-local.sh" --package "$package"
)"
grep -Fq 'install-local: source=package' <<<"$package_output"
grep -Fq "install-local: version=$version" <<<"$package_output"
grep -Fq 'install-local: result=verified' <<<"$package_output"
cmp -s \
  "$test_root/BenheimQoL.dll" \
  "$packaged_game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll"
grep -Fqx "$version" "$packaged_game_dir/BepInEx/plugins/BenheimQoL/VERSION"

# A directory/version mismatch fails before the shipped installer can mutate
# the selected game directory.
bad_package_root="$test_root/bad-package/Benheim-macOS-$version"
bad_package="$test_root/Benheim-macOS-mismatched.zip"
mkdir -p "$(dirname "$bad_package_root")"
unzip -qq "$package" -d "$test_root/bad-package"
printf '9.9.9\n' > "$bad_package_root/VERSION"
(
  cd "$test_root/bad-package"
  zip -qr "$bad_package" "Benheim-macOS-$version"
)
bad_game_dir="$test_root/Bad Package Valheim"
mkdir -p "$bad_game_dir/valheim.app/Contents/Resources"
touch "$bad_game_dir/valheim.app/Contents/Resources/PlayerIcon.icns"
if PATH="$mock_bin:$PATH" \
  BENHEIM_QOL_GAME_DIR="$bad_game_dir" \
  BENHEIM_QOL_APP_DIR="$test_root/Bad Package Applications" \
  BENHEIM_QOL_BEPINEX_URL="file://$fixture_zip" \
  BENHEIM_QOL_BEPINEX_SHA256="$fixture_sha" \
    "$root/scripts/install-local.sh" --package "$bad_package" >/dev/null 2>&1; then
  echo "local package install accepted a mismatched VERSION" >&2
  exit 1
fi
test ! -e "$bad_game_dir/BepInEx"

echo "macOS installer, migration, and package checks passed"
