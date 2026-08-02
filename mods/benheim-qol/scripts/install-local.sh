#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dll="$root/src/bin/Release/netstandard2.1/BenheimQoL.dll"

"$root/scripts/build.sh"

if [[ ! -f "$dll" ]]; then
  echo "Missing build output: $dll" >&2
  exit 1
fi

BENHEIM_QOL_DLL="$dll" \
BENHEIM_QOL_LAUNCHER_SOURCE="$root/scripts/macos-launcher.sh" \
BENHEIM_QOL_NONINTERACTIVE=1 \
  "$root/scripts/install-macos.command"
