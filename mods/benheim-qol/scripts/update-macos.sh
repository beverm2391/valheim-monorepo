#!/bin/bash
set -euo pipefail

asset_name="${BENHEIM_UPDATE_ASSET:-Benheim-macOS.zip}"
release_base="${BENHEIM_UPDATE_BASE_URL:-https://github.com/beverm2391/valheim-server/releases/latest/download}"
checksums_url="${BENHEIM_UPDATE_SHA256SUMS_URL:-${release_base%/}/SHA256SUMS.txt}"
game_dir="${BENHEIM_QOL_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
plugin="$game_dir/BepInEx/plugins/BenheimQoL/BenheimQoL.dll"
tmp_dir="$(mktemp -d)"

cleanup() {
  rm -rf "$tmp_dir"
}
trap cleanup EXIT

show_dialog() {
  title=$1
  message=$2

  if [[ "${BENHEIM_UPDATE_NO_UI:-0}" == "1" ]]; then
    return
  fi

  /usr/bin/osascript - "$title" "$message" <<'APPLESCRIPT' >/dev/null 2>&1 || true
on run argv
  display dialog (item 2 of argv) with title (item 1 of argv) buttons {"OK"} default button "OK"
end run
APPLESCRIPT
}

fail() {
  echo "Update failed: $1" >&2
  show_dialog "Benheim update failed" "$1"
  exit 1
}

if pgrep -x valheim >/dev/null 2>&1 \
  || pgrep -x valheim.x86_64 >/dev/null 2>&1 \
  || pgrep -f "$game_dir/valheim.app/Contents/MacOS" >/dev/null 2>&1; then
  fail "Quit Valheim completely, then open Update Benheim again."
fi

if [[ ! -f "$plugin" ]]; then
  fail "Benheim is not installed normally. Download the latest Mac package and run Install Benheim.command."
fi

echo "Checking for a Benheim update..."
if ! curl -fsSL --retry 2 --connect-timeout 10 --max-time 60 \
  "$checksums_url" -o "$tmp_dir/SHA256SUMS.txt"; then
  fail "Could not reach the Benheim release. Your current installation was not changed."
fi

expected_sha256="$(awk -v file="$asset_name" \
  '$2 == file || $2 == ("*" file) { print tolower($1); exit }' \
  "$tmp_dir/SHA256SUMS.txt")"
if [[ ! "$expected_sha256" =~ ^[0-9a-f]{64}$ ]]; then
  fail "The latest release does not contain a checksum for $asset_name. Your current installation was not changed."
fi

archive="$tmp_dir/$asset_name"
if ! curl -fsSL --retry 2 --connect-timeout 10 --max-time 300 \
  "${release_base%/}/$asset_name" -o "$archive"; then
  fail "The update download did not finish. Your current installation was not changed."
fi

actual_sha256="$(shasum -a 256 "$archive" | awk '{print tolower($1)}')"
if [[ "$actual_sha256" != "$expected_sha256" ]]; then
  fail "The update checksum did not match. Your current installation was not changed."
fi

expanded="$tmp_dir/expanded"
mkdir "$expanded"
if ! unzip -q "$archive" -d "$expanded"; then
  fail "The update package could not be opened. Your current installation was not changed."
fi

installer_count="$(find "$expanded" -mindepth 2 -maxdepth 2 -type f -name 'Install Benheim.command' | wc -l | tr -d ' ')"
if [[ "$installer_count" != "1" ]]; then
  fail "The update package has an unexpected layout. Your current installation was not changed."
fi

installer="$(find "$expanded" -mindepth 2 -maxdepth 2 -type f -name 'Install Benheim.command')"
package_dir="$(dirname "$installer")"
package_plugin="$package_dir/BenheimQoL.dll"
if [[ ! -f "$package_plugin" || ! -f "$package_dir/macos-launcher.sh" || ! -f "$package_dir/update-macos.sh" ]]; then
  fail "The update package is incomplete. Your current installation was not changed."
fi

installed_sha256="$(shasum -a 256 "$plugin" | awk '{print tolower($1)}')"
package_sha256="$(shasum -a 256 "$package_plugin" | awk '{print tolower($1)}')"
if [[ "$installed_sha256" == "$package_sha256" ]]; then
  message="Benheim is already up to date."
  echo "$message"
  show_dialog "Benheim" "$message"
  exit 0
fi

echo "Installing the verified update..."
if ! BENHEIM_QOL_NONINTERACTIVE=1 "$installer"; then
  fail "The installer could not finish the update. Download the latest Mac package and run Install Benheim.command."
fi

message="Benheim was updated. You can open Benheim normally."
echo "$message"
show_dialog "Benheim" "$message"
