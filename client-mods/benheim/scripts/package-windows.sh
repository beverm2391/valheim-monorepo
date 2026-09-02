#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
dll="${BENHEIM_QOL_DLL:-$root/src/bin/Release/netstandard2.1/BenheimQoL.dll}"
dist="${BENHEIM_QOL_DIST:-$root/dist}"
private_diagnostics_config="${BENHEIM_QOL_PRIVATE_DIAGNOSTICS_CONFIG:-}"
source_commit="${BENHEIM_QOL_SOURCE_COMMIT:-}"
package_name="Benheim-Windows-$version"
if [[ -n "$private_diagnostics_config" ]]; then
  package_name="Benheim-PRIVATE-TEST-Windows-$version"
  if [[ ! -f "$private_diagnostics_config" ]] ||
    [[ "$(sed -n '1p' "$private_diagnostics_config")" != "BENHEIM_PRIVATE_DIAGNOSTICS_V1" ]]; then
    echo "The private-test diagnostics config is missing or invalid." >&2
    exit 1
  fi
fi
if [[ -n "$source_commit" && ! "$source_commit" =~ ^[0-9a-f]{40,64}$ ]]; then
  echo "BENHEIM_QOL_SOURCE_COMMIT must be an exact Git commit." >&2
  exit 1
fi
stage="$dist/$package_name"

if [[ -z "$version" ]]; then
  echo "Could not determine the Benheim version." >&2
  exit 1
fi

if [[ "${BENHEIM_QOL_SKIP_BUILD:-0}" != "1" ]]; then
  "$root/scripts/build.sh"
fi

if [[ ! -f "$dll" ]]; then
  echo "The Benheim plugin file was not found at: $dll" >&2
  exit 1
fi

rm -rf "$stage" "$dist/$package_name.zip"
install -d "$stage"
install -m 0644 "$root/scripts/Install Benheim.cmd" "$stage/Install Benheim.cmd"
install -m 0644 "$root/scripts/install-windows.ps1" "$stage/install-windows.ps1"
install -m 0644 "$root/scripts/launch-windows.ps1" "$stage/launch-windows.ps1"
install -m 0644 "$root/scripts/windows-doorstop-config.ps1" "$stage/windows-doorstop-config.ps1"
install -m 0644 "$dll" "$stage/BenheimQoL.dll"
printf '%s\n' "$version" > "$stage/VERSION"
if [[ -n "$private_diagnostics_config" ]]; then
  install -m 0600 "$private_diagnostics_config" "$stage/PRIVATE-TEST-DIAGNOSTICS.cfg"
fi
if [[ -n "$source_commit" ]]; then
  printf '%s\n' "$source_commit" > "$stage/SOURCE_COMMIT"
fi

(
  cd "$dist"
  zip -qr "$package_name.zip" "$package_name"
)

echo "$dist/$package_name.zip"
