#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
dll="${BENHEIM_QOL_DLL:-$root/src/bin/Release/netstandard2.1/BenheimQoL.dll}"
dist="${BENHEIM_QOL_DIST:-$root/dist}"
private_diagnostics_config="${BENHEIM_QOL_PRIVATE_DIAGNOSTICS_CONFIG:-}"
private_speech_config="${BENHEIM_QOL_PRIVATE_SPEECH_CONFIG:-}"
source_commit="${BENHEIM_QOL_SOURCE_COMMIT:-}"
package_name="Benheim-Windows-$version"
if [[ -z "$private_diagnostics_config" || ! -f "$private_diagnostics_config" ]]; then
  echo "Group packages require an Axiom diagnostics config." >&2
  exit 1
fi
if [[ ! "$source_commit" =~ ^[0-9a-f]{40,64}$ ]]; then
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
expected_build_id="sha256:$(shasum -a 256 "$dll" | awk '{print $1}')"
if [[ "$(wc -l < "$private_diagnostics_config" | tr -d ' ')" != "5" ]] ||
  [[ "$(sed -n '1p' "$private_diagnostics_config")" != "BENHEIM_PRIVATE_DIAGNOSTICS_V1" ]] ||
  ! grep -Eq '^endpoint=https://[^/?#]+$' "$private_diagnostics_config" ||
  ! grep -Eq '^dataset=[A-Za-z0-9_.-]+$' "$private_diagnostics_config" ||
  ! grep -Eq '^token=.+$' "$private_diagnostics_config" ||
  ! grep -Fxq "build_id=$expected_build_id" "$private_diagnostics_config"; then
  echo "The Axiom diagnostics config is invalid or does not match the package DLL." >&2
  exit 1
fi
if [[ -n "$private_speech_config" ]]; then
  if [[ ! -f "$private_speech_config" ]] ||
    [[ "$(wc -c < "$private_speech_config" | tr -d ' ')" -gt 4096 ]] ||
    [[ "$(wc -l < "$private_speech_config" | tr -d ' ')" != "2" ]] ||
    [[ "$(sed -n '1p' "$private_speech_config")" != "BENHEIM_PRIVATE_SPEECH_V1" ]] ||
    ! grep -Eq '^api_key=[^[:space:]]+$' "$private_speech_config" ||
    ! awk 'length($0) > 1032 { exit 1 }' "$private_speech_config"; then
    echo "The private resident speech config is invalid." >&2
    exit 1
  fi
fi

rm -rf "$stage" "$dist/$package_name.zip"
install -d "$stage"
install -m 0644 "$root/scripts/Install Benheim.cmd" "$stage/Install Benheim.cmd"
install -m 0644 "$root/scripts/install-windows.ps1" "$stage/install-windows.ps1"
install -m 0644 "$root/scripts/launch-windows.ps1" "$stage/launch-windows.ps1"
install -m 0644 "$root/scripts/windows-doorstop-config.ps1" "$stage/windows-doorstop-config.ps1"
install -m 0644 "$root/scripts/windows-resident-speech.ps1" "$stage/windows-resident-speech.ps1"
install -m 0644 "$dll" "$stage/BenheimQoL.dll"
printf '%s\n' "$version" > "$stage/VERSION"
install -m 0600 "$private_diagnostics_config" "$stage/AXIOM-DIAGNOSTICS.cfg"
if [[ -n "$private_speech_config" ]]; then
  install -m 0600 "$private_speech_config" "$stage/GEORGE-SPEECH.cfg"
fi
printf '%s\n' "$source_commit" > "$stage/SOURCE_COMMIT"

(
  cd "$dist"
  zip -qr "$package_name.zip" "$package_name"
)

echo "$dist/$package_name.zip"
