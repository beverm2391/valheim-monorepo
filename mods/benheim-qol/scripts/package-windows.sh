#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
dll="${BENHEIM_QOL_DLL:-$root/src/bin/Release/netstandard2.1/BenheimQoL.dll}"
dist="${BENHEIM_QOL_DIST:-$root/dist}"
package_name="BenheimQoL-Windows-$version"
stage="$dist/$package_name"

if [[ -z "$version" ]]; then
  echo "Could not determine BenheimQoL version." >&2
  exit 1
fi

if [[ "${BENHEIM_QOL_SKIP_BUILD:-0}" != "1" ]]; then
  "$root/scripts/build.sh"
fi

if [[ ! -f "$dll" ]]; then
  echo "BenheimQoL.dll was not found at: $dll" >&2
  exit 1
fi

rm -rf "$stage" "$dist/$package_name.zip"
install -d "$stage"
install -m 0644 "$root/scripts/Install BenheimQoL.cmd" "$stage/Install BenheimQoL.cmd"
install -m 0644 "$root/scripts/install-windows.ps1" "$stage/install-windows.ps1"
install -m 0644 "$dll" "$stage/BenheimQoL.dll"

(
  cd "$dist"
  zip -qr "$package_name.zip" "$package_name"
)

echo "$dist/$package_name.zip"
