#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
installer="$root/scripts/install-windows.ps1"
wrapper="$root/scripts/Install Benheim.cmd"
updater="$root/scripts/update-windows.ps1"
updater_wrapper="$root/scripts/Update Benheim.cmd"
package_script="$root/scripts/package-windows.sh"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

grep -Fq "Get-Process -Name 'valheim'" "$installer"
grep -Fq "libraryfolders.vdf" "$installer"
grep -Fq "BENHEIM_QOL_GAME_DIR" "$installer"
grep -Fq "Get-FileHash -LiteralPath \$archive -Algorithm SHA256" "$installer"
grep -Fq "winhttp.dll" "$installer"
grep -Fq "BepInEx\\core\\BepInEx.dll" "$installer"
grep -Fq "plugins\\MassFarming\\MassFarming.dll" "$installer"
grep -Fq "Refusing to replace an unrelated shortcut" "$installer"
grep -Fq "Join-Path \$desktop 'Benheim.lnk'" "$installer"
grep -Fq "Join-Path \$desktop 'Benheim QoL.lnk'" "$installer"
grep -Fq "Join-Path \$desktop 'Update Benheim.lnk'" "$installer"
grep -Fq "Refusing to replace an unrelated or damaged updater directory" "$installer"
grep -Fq "\$updaterVersionsRoot = Join-Path \$updaterRoot 'versions'" "$installer"
grep -Fq "Refusing to replace a damaged updater versions directory" "$installer"
grep -Fq "\$updaterShortcut.WorkingDirectory = [System.IO.Path]::GetTempPath()" "$installer"
grep -Fq "Valheim started during setup" "$installer"
grep -Fq "Copy-Item -LiteralPath \$pluginBackup -Destination \$pluginPath -Force" "$installer"
grep -Fq "SHA256SUMS.txt" "$updater"
grep -Fq "Get-FileHash -LiteralPath \$archive -Algorithm SHA256" "$updater"
grep -Fq "already up to date" "$updater"
grep -Fq "Your current installation was not changed" "$updater"
grep -Fq "steam://rungameid/892970" "$installer"
grep -Fq "valheim.exe') + ',0'" "$installer"
grep -Fq -- "-ExecutionPolicy Bypass" "$wrapper"
grep -Fq -- "-ExecutionPolicy Bypass" "$updater_wrapper"
grep -Fq "package_name=\"Benheim-Windows-\$version\"" "$package_script"

printf 'test-dll\n' > "$test_root/BenheimQoL.dll"
BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
BENHEIM_QOL_DIST="$test_root/dist" \
BENHEIM_QOL_SKIP_BUILD=1 \
  "$package_script" >/dev/null

package="$test_root/dist/Benheim-Windows-$version.zip"
test -f "$package"
unzip -Z1 "$package" | grep -Fqx "Benheim-Windows-$version/BenheimQoL.dll"
unzip -Z1 "$package" | grep -Fqx "Benheim-Windows-$version/Install Benheim.cmd"
unzip -Z1 "$package" | grep -Fqx "Benheim-Windows-$version/install-windows.ps1"
unzip -Z1 "$package" | grep -Fqx "Benheim-Windows-$version/Update Benheim.cmd"
unzip -Z1 "$package" | grep -Fqx "Benheim-Windows-$version/update-windows.ps1"

echo "Windows installer source and package checks passed"
