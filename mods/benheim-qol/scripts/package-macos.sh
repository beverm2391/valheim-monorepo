#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
dll="$root/src/bin/Release/netstandard2.1/BenheimQoL.dll"
dist="$root/dist"
package_name="BenheimQoL-macOS-$version"
stage="$dist/$package_name"

if [[ -z "$version" ]]; then
  echo "Could not determine BenheimQoL version." >&2
  exit 1
fi

"$root/scripts/build.sh"
rm -rf "$stage" "$dist/$package_name.zip"
install -d "$stage"
install -m 0755 "$root/scripts/install-macos.command" "$stage/Install BenheimQoL.command"
install -m 0755 "$root/scripts/macos-launcher.sh" "$stage/macos-launcher.sh"
install -m 0644 "$dll" "$stage/BenheimQoL.dll"

(
  cd "$dist"
  zip -qr "$package_name.zip" "$package_name"
)

echo "$dist/$package_name.zip"
