#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
process_check="$root/scripts/check-valheim-stopped.sh"
tmp_path=""

cleanup() {
  if [[ -n "$tmp_path" && -e "$tmp_path" ]]; then
    find "$tmp_path" -depth -delete
  fi
}
trap cleanup EXIT

fail() {
  echo "$1" >&2
  exit 1
}

usage() {
  cat <<EOF
Usage:
  $0
  $0 --package /path/to/Benheim-macOS-X.Y.Z.zip
EOF
}

require_valheim_stopped() {
  [[ -x "$process_check" ]] \
    || fail "The repo-owned Valheim process check is missing or not executable."
  if ! "$process_check" --quiet; then
    fail "Valheim is running. Quit it completely before installing Benheim."
  fi
}

install_source_build() {
  local dll="$root/src/bin/Release/netstandard2.1/BenheimQoL.dll"
  local version
  local version_file
  version="$(sed -n 's/.*PluginVersion = "\([^"]*\)".*/\1/p' "$root/src/Plugin.cs")"
  version_file="$(mktemp)"
  tmp_path="$version_file"

  "$root/scripts/build.sh"
  [[ -f "$dll" ]] || fail "Missing build output: $dll"
  printf '%s\n' "$version" > "$version_file"

  BENHEIM_QOL_DLL="$dll" \
  BENHEIM_QOL_VERSION_FILE="$version_file" \
  BENHEIM_QOL_LAUNCHER_SOURCE="$root/scripts/macos-launcher.sh" \
  BENHEIM_QOL_NONINTERACTIVE=1 \
    "$root/scripts/install-macos.command"
}

install_package() {
  local package="$1"
  local entries duplicate_entries package_root roots expected_version
  local private_package=0
  local entry payload packaged_version installer game_dir installed_dir
  local packaged_sha installed_sha

  [[ -f "$package" ]] || fail "The selected macOS package was not found: $package"
  package="$(cd "$(dirname "$package")" && pwd)/$(basename "$package")"
  unzip -tq "$package" >/dev/null || fail "The selected macOS package is not a valid ZIP archive."

  entries="$(unzip -Z1 "$package")"
  duplicate_entries="$(printf '%s\n' "$entries" | sort | uniq -d)"
  [[ -z "$duplicate_entries" ]] || fail "The selected macOS package contains duplicate paths."
  roots="$(printf '%s\n' "$entries" | awk -F/ 'NF { print $1 }' | sort -u)"
  [[ "$(printf '%s\n' "$roots" | wc -l | tr -d ' ')" == "1" ]] \
    || fail "The selected macOS package must contain one top-level directory."
  package_root="$roots"

  if [[ "$package_root" =~ ^Benheim-macOS-([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
    expected_version="${BASH_REMATCH[1]}"
  elif [[ "$package_root" =~ ^Benheim-PRIVATE-TEST-macOS-([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
    expected_version="${BASH_REMATCH[1]}"
    private_package=1
  else
    fail "The selected ZIP does not contain a versioned Benheim macOS package."
  fi

  for required in \
    "$package_root/" \
    "$package_root/BenheimQoL.dll" \
    "$package_root/Install Benheim.command" \
    "$package_root/VERSION" \
    "$package_root/check-valheim-stopped.sh" \
    "$package_root/macos-launcher.sh"; do
    grep -Fxq "$required" <<<"$entries" \
      || fail "The selected macOS package is missing: $required"
  done
  if [[ "$private_package" == "1" ]]; then
    grep -Fxq "$package_root/PRIVATE-TEST-DIAGNOSTICS.cfg" <<<"$entries" \
      || fail "The selected private-test package is missing its diagnostics configuration."
  fi

  while IFS= read -r entry; do
    case "$entry" in
      "$package_root/"|\
      "$package_root/BenheimQoL.dll"|\
      "$package_root/Install Benheim.command"|\
      "$package_root/VERSION"|\
      "$package_root/check-valheim-stopped.sh"|\
      "$package_root/macos-launcher.sh"|\
      "$package_root/SOURCE_COMMIT") ;;
      "$package_root/PRIVATE-TEST-DIAGNOSTICS.cfg")
        [[ "$private_package" == "1" ]] \
          || fail "A public Benheim package cannot contain private diagnostics configuration."
        ;;
      *) fail "The selected macOS package contains an unexpected path: $entry" ;;
    esac
  done <<<"$entries"

  tmp_path="$(mktemp -d)"
  unzip -q "$package" -d "$tmp_path"
  payload="$tmp_path/$package_root"
  [[ -d "$payload" && ! -L "$payload" ]] \
    || fail "The selected macOS package contains an unsafe top-level directory."
  for required_file in \
    "BenheimQoL.dll" \
    "Install Benheim.command" \
    "VERSION" \
    "check-valheim-stopped.sh" \
    "macos-launcher.sh"; do
    [[ -f "$payload/$required_file" && ! -L "$payload/$required_file" ]] \
      || fail "The selected macOS package contains an unsafe file: $required_file"
  done
  for optional_file in "SOURCE_COMMIT" "PRIVATE-TEST-DIAGNOSTICS.cfg"; do
    if [[ -e "$payload/$optional_file" ]]; then
      [[ -f "$payload/$optional_file" && ! -L "$payload/$optional_file" ]] \
        || fail "The selected macOS package contains an unsafe file: $optional_file"
    fi
  done
  [[ -x "$payload/Install Benheim.command" ]] \
    || fail "The packaged Mac installer is not executable."
  [[ -x "$payload/check-valheim-stopped.sh" ]] \
    || fail "The packaged Valheim process check is not executable."

  packaged_version="$(tr -d '\r\n' < "$payload/VERSION")"
  [[ "$packaged_version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] \
    || fail "The packaged VERSION is invalid."
  [[ "$packaged_version" == "$expected_version" ]] \
    || fail "The package directory and VERSION do not match."

  installer="$payload/Install Benheim.command"
  env \
    -u BENHEIM_QOL_DLL \
    -u BENHEIM_QOL_VERSION_FILE \
    -u BENHEIM_QOL_LAUNCHER_SOURCE \
    -u BENHEIM_QOL_PRIVATE_DIAGNOSTICS_FILE \
    BENHEIM_QOL_NONINTERACTIVE=1 \
    "$installer"

  game_dir="${BENHEIM_QOL_GAME_DIR:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
  installed_dir="$game_dir/BepInEx/plugins/BenheimQoL"
  [[ -f "$installed_dir/VERSION" ]] \
    || fail "The installed Benheim VERSION file is missing."
  [[ "$(tr -d '\r\n' < "$installed_dir/VERSION")" == "$packaged_version" ]] \
    || fail "The installed Benheim version does not match the selected package."
  cmp -s "$payload/BenheimQoL.dll" "$installed_dir/BenheimQoL.dll" \
    || fail "The installed Benheim DLL does not match the selected package."

  packaged_sha="$(shasum -a 256 "$payload/BenheimQoL.dll" | awk '{print $1}')"
  installed_sha="$(shasum -a 256 "$installed_dir/BenheimQoL.dll" | awk '{print $1}')"
  [[ "$packaged_sha" == "$installed_sha" ]]
  printf 'install-local: source=package\n'
  printf 'install-local: version=%s\n' "$packaged_version"
  printf 'install-local: dll_sha256=%s\n' "$installed_sha"
  printf 'install-local: result=verified\n'
}

case "${1:-}" in
  --help|-h)
    [[ "$#" == "1" ]] || { usage >&2; exit 64; }
    usage
    exit 0
    ;;
  --package)
    [[ "$#" == "2" ]] || { usage >&2; exit 64; }
    require_valheim_stopped
    install_package "$2"
    ;;
  "")
    [[ "$#" == "0" ]] || { usage >&2; exit 64; }
    require_valheim_stopped
    install_source_build
    ;;
  *)
    usage >&2
    exit 64
    ;;
esac
