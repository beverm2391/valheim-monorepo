#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
installer="$root/scripts/install-windows.ps1"
wrapper="$root/scripts/Install BenheimQoL.cmd"
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
grep -Fq "steam://rungameid/892970" "$installer"
grep -Fq "valheim.exe') + ',0'" "$installer"
grep -Fq -- "-ExecutionPolicy Bypass" "$wrapper"
grep -Fq "package_name=\"BenheimQoL-Windows-\$version\"" "$package_script"

printf 'test-dll\n' > "$test_root/BenheimQoL.dll"
BENHEIM_QOL_DLL="$test_root/BenheimQoL.dll" \
BENHEIM_QOL_DIST="$test_root/dist" \
BENHEIM_QOL_SKIP_BUILD=1 \
  "$package_script" >/dev/null

package="$test_root/dist/BenheimQoL-Windows-$version.zip"
test -f "$package"
unzip -Z1 "$package" | grep -Fqx "BenheimQoL-Windows-$version/BenheimQoL.dll"
unzip -Z1 "$package" | grep -Fqx "BenheimQoL-Windows-$version/Install BenheimQoL.cmd"
unzip -Z1 "$package" | grep -Fqx "BenheimQoL-Windows-$version/install-windows.ps1"

echo "Windows installer source and package checks passed"
