#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dll="$root/src/bin/Release/netstandard2.1/BenheimQoL.dll"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
version_file="$(mktemp)"
trap 'rm -f "$version_file"' EXIT

"$root/scripts/build.sh"

if [[ ! -f "$dll" ]]; then
  echo "Missing build output: $dll" >&2
  exit 1
fi
printf '%s\n' "$version" > "$version_file"

BENHEIM_QOL_DLL="$dll" \
BENHEIM_QOL_VERSION_FILE="$version_file" \
BENHEIM_QOL_LAUNCHER_SOURCE="$root/scripts/macos-launcher.sh" \
BENHEIM_QOL_NONINTERACTIVE=1 \
  "$root/scripts/install-macos.command"
