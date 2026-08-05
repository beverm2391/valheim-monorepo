#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
installer="$root/scripts/install-windows.ps1"
wrapper="$root/scripts/Install Benheim.cmd"
launcher="$root/scripts/launch-windows.ps1"
doorstop_helpers="$root/scripts/windows-doorstop-config.ps1"
package_script="$root/scripts/package-windows.sh"
rollback_test="$root/tests/windows-doorstop-rollback-test.ps1"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

grep -Fq "Get-Process -Name 'valheim'" "$installer"
grep -Fq 'libraryfolders.vdf' "$installer"
grep -Fq 'BENHEIM_QOL_GAME_DIR' "$installer"
grep -Fq 'Get-FileHash -LiteralPath $archive -Algorithm SHA256' "$installer"
grep -Fq 'winhttp.dll' "$installer"
grep -Fq 'BepInEx\core\BepInEx.dll' "$installer"
grep -Fq 'plugins\MassFarming\MassFarming.dll' "$installer"
grep -Fq 'Set-DoorstopDisabled' "$installer"
grep -Fq "'\${1}false'" "$doorstop_helpers"
grep -Fq "Join-Path \$desktop 'Benheim.lnk'" "$installer"
grep -Fq "Join-Path \$desktop 'Benheim QoL.lnk'" "$installer"
grep -Fq "Join-Path \$desktop 'Update Benheim.lnk'" "$installer"
grep -Fq "Join-Path \$localAppData 'BenheimLauncher'" "$installer"
grep -Fq "Join-Path \$localAppData 'Benheim'" "$installer"
grep -Fq 'Benheim updater managed directory v1' "$installer"
grep -Fq 'Refusing to replace an unrelated or damaged launcher directory' "$installer"
grep -Fq 'Refusing to replace an unrelated shortcut' "$installer"
grep -Fq 'Valheim started during setup' "$installer"
grep -Fq 'Copy-Item -LiteralPath $pluginBackup -Destination $pluginPath -Force' "$installer"
grep -Fq 'Copy-Item -LiteralPath $versionBackup -Destination $versionPath -Force' "$installer"
grep -Fq 'Save-DoorstopConfig' "$installer"
grep -Fq 'Restore-DoorstopConfig' "$installer"
grep -Fq '. $DoorstopConfigHelpers' "$installer"
grep -Fq "Join-Path \$TempDir 'doorstop_config.previous.ini'" "$installer"
grep -Fq '$doorstopConfigTouched = $true' "$installer"
grep -Fq -- '-HadPrevious $doorstopConfigHadPrevious' "$installer"
grep -Fq -- '-ExecutionPolicy Bypass' "$wrapper"

snapshot_line="$(grep -nF '$doorstopConfigHadPrevious = Save-DoorstopConfig' "$installer" | cut -d: -f1)"
touch_line="$(grep -nF '$doorstopConfigTouched = $true' "$installer" | cut -d: -f1)"
copy_line="$(grep -nF 'Get-ChildItem -LiteralPath $bepInExRoot -Force |' "$installer" | cut -d: -f1)"
restore_line="$(grep -nF '            Restore-DoorstopConfig `' "$installer" | cut -d: -f1)"
test "$snapshot_line" -lt "$touch_line"
test "$touch_line" -lt "$copy_line"
test "$copy_line" -lt "$restore_line"

if command -v pwsh >/dev/null 2>&1; then
  pwsh -NoProfile -File "$rollback_test"
elif command -v powershell.exe >/dev/null 2>&1; then
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$rollback_test"
else
  grep -Fq '[System.IO.File]::WriteAllBytes($configPath, $originalBytes)' "$rollback_test"
  grep -Fq 'Rollback did not restore the previous config exactly.' "$rollback_test"
  grep -Fq 'Rollback retained a config that did not exist before installation.' "$rollback_test"
  grep -Fq 'Successful mutation did not retain enabled=false.' "$rollback_test"
fi

grep -Fq 'libraryfolders.vdf' "$launcher"
grep -Fq "Get-Process -Name 'steam'" "$launcher"
grep -Fq "Join-Path \$gameDir 'valheim.exe'" "$launcher"
grep -Fq -- "-ArgumentList '--doorstop-enabled', 'true'" "$launcher"
grep -Fq 'enabled\s*=\s*false' "$launcher"
! grep -Eq 'Invoke-WebRequest|github|steam://|Update and launch|Launch current version' "$launcher"
! grep -Eq 'Rename-Item|winhttp\.dll\.(disabled|bak)' "$installer" "$launcher"

test ! -e "$root/scripts/Update Benheim.cmd"
test ! -e "$root/scripts/update-windows.ps1"
! grep -Eq 'Update Benheim\.cmd|update-windows\.ps1' "$package_script"

printf 'test-dll\n' > "$test_root/BenheimQoL.dll"
BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
BENHEIM_QOL_DIST="$test_root/dist" \
BENHEIM_QOL_SKIP_BUILD=1 \
  "$package_script" >/dev/null

package="$test_root/dist/Benheim-Windows-$version.zip"
package_entries="$(unzip -Z1 "$package" | sort)"
expected_entries="$(printf '%s\n' \
  "Benheim-Windows-$version/" \
  "Benheim-Windows-$version/BenheimQoL.dll" \
  "Benheim-Windows-$version/Install Benheim.cmd" \
  "Benheim-Windows-$version/VERSION" \
  "Benheim-Windows-$version/install-windows.ps1" \
  "Benheim-Windows-$version/launch-windows.ps1" \
  "Benheim-Windows-$version/windows-doorstop-config.ps1" | sort)"
test "$package_entries" = "$expected_entries"

echo "Windows vanilla/modded launcher migration and package checks passed"
