#!/usr/bin/env bash
set -euo pipefail

# Verify and build once, then let the existing platform packagers consume that
# exact Release DLL without rebuilding it.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dll="$root/src/bin/Release/netstandard2.1/BenheimQoL.dll"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
dist="$root/dist"
mac_package="$dist/Benheim-macOS-$version.zip"
windows_package="$dist/Benheim-Windows-$version.zip"
complete=0
cleanup() {
  if [[ "$complete" != "1" ]]; then
    rm -f -- "$mac_package" "$windows_package"
  fi
}
trap cleanup EXIT

rm -f -- "$mac_package" "$windows_package"

"$root/scripts/verify.sh"
if [[ ! -f "$dll" ]]; then
  echo "The verified Benheim Release DLL was not found at: $dll" >&2
  exit 1
fi

env -u BENHEIM_QOL_PRIVATE_DIAGNOSTICS_CONFIG \
  BENHEIM_QOL_DLL="$dll" BENHEIM_QOL_DIST="$dist" BENHEIM_QOL_SKIP_BUILD=1 \
  "$root/scripts/package-macos.sh"
env -u BENHEIM_QOL_PRIVATE_DIAGNOSTICS_CONFIG \
  BENHEIM_QOL_DLL="$dll" BENHEIM_QOL_DIST="$dist" BENHEIM_QOL_SKIP_BUILD=1 \
  "$root/scripts/package-windows.sh"

complete=1
