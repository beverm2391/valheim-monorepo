#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<EOF
Usage: $0 [--validate-only] ARCHIVE_OR_DIRECTORY [SERVER_INSTALL_DIRECTORY]

Inspect a complete worlds_local backup archive or an extracted worlds_local
directory. SERVER_INSTALL_DIRECTORY defaults to /opt/valheim/server when that
directory exists and is used only to report the installed Steam build metadata.
EOF
}

die() {
  echo "Error: $*" >&2
  exit 1
}

sha256_file() {
  local path=$1

  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$path" | awk '{print $1}'
  elif command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$path" | awk '{print $1}'
  else
    die "sha256sum or shasum is required"
  fi
}

file_size() {
  local path=$1

  if stat -c '%s' "$path" >/dev/null 2>&1; then
    stat -c '%s' "$path"
  else
    stat -f '%z' "$path"
  fi
}

validate_archive() {
  local archive=$1
  local listing=$2
  local member normalized first_component
  local payload_entries=0
  local sole_top_level=""

  [[ -f "$archive" ]] || die "archive does not exist: $archive"
  if ! tar -tzf "$archive" > "$listing"; then
    die "archive is not a readable gzip-compressed tar file: $archive"
  fi

  if ! tar -tvzf "$archive" | awk '
    substr($1, 1, 1) != "-" && substr($1, 1, 1) != "d" { exit 1 }
  '; then
    die "archive contains a member that is not a regular file or directory"
  fi

  while IFS= read -r member; do
    [[ "$member" != /* ]] || die "archive contains an absolute path: $member"

    normalized=$member
    while [[ "$normalized" == ./* ]]; do
      normalized=${normalized#./}
    done

    [[ -n "$normalized" && "$normalized" != "." ]] || continue
    case "/$normalized/" in
      *"/../"*) die "archive contains a parent-directory path: $member" ;;
    esac

    first_component=${normalized%%/*}
    if [[ -z "$sole_top_level" ]]; then
      sole_top_level=$first_component
    elif [[ "$sole_top_level" != "$first_component" ]]; then
      sole_top_level="__multiple__"
    fi
    payload_entries=$((payload_entries + 1))
  done < "$listing"

  (( payload_entries > 0 )) || die "archive contains no world storage entries"
  if [[ "$sole_top_level" == "worlds_local" ]]; then
    die "archive has a top-level worlds_local wrapper; archive its contents instead: tar -C /var/lib/valheim/worlds_local -czf ARCHIVE ."
  fi
}

inspect_directory() {
  local root=$1
  local legacy_files directory_roots files directories kibibytes layout
  local candidate relative size digest

  [[ -d "$root" ]] || die "directory does not exist: $root"

  legacy_files=$(find "$root" -mindepth 1 -maxdepth 1 -type f \( -name '*.db' -o -name '*.fwl' \) | wc -l | tr -d ' ')
  directory_roots=$(find "$root" -mindepth 1 -maxdepth 1 -type d | wc -l | tr -d ' ')
  files=$(find "$root" -type f | wc -l | tr -d ' ')
  directories=$(find "$root" -mindepth 1 -type d | wc -l | tr -d ' ')
  kibibytes=$(du -sk "$root" | awk '{print $1}')

  if (( legacy_files > 0 && directory_roots == 0 )); then
    layout="legacy files"
  elif (( legacy_files == 0 && directory_roots > 0 )); then
    layout="directory-based"
  elif (( legacy_files > 0 && directory_roots > 0 )); then
    layout="mixed contents preserved from archive"
  else
    layout="unclassified"
  fi

  echo "Storage directory: $root"
  echo "Storage layout: $layout"
  echo "Regular files: $files"
  echo "Directories: $directories"
  echo "Apparent size (KiB): $kibibytes"
  echo "Top-level entries:"
  find "$root" -mindepth 1 -maxdepth 1 -exec basename {} \; | LC_ALL=C sort | sed 's/^/  /'

  echo "Metadata candidates:"
  local found_metadata=0
  while IFS= read -r candidate; do
    found_metadata=1
    relative=${candidate#"$root"/}
    size=$(file_size "$candidate")
    digest=$(sha256_file "$candidate")
    printf '  %s bytes  sha256=%s  %s\n' "$size" "$digest" "$relative"
  done < <(
    find "$root" -type f \( \
      -name '*.fwl' -o \
      -iname '*metadata*' -o \
      -iname '*manifest*' -o \
      -iname '*meta' -o \
      -iname 'meta*' \
    \) | LC_ALL=C sort
  )
  if (( found_metadata == 0 )); then
    echo "  none recognized; the complete storage directory remains authoritative"
  fi
}

inspect_build() {
  local server_dir=$1
  local manifest="$server_dir/steamapps/appmanifest_896660.acf"

  echo "Server build metadata:"
  if [[ ! -f "$manifest" ]]; then
    echo "  not found at $manifest"
    return
  fi

  echo "  manifest: $manifest"
  awk '
    /^[[:space:]]*"(appid|name|buildid|LastUpdated|StateFlags)"/ {
      key=$1
      $1=""
      sub(/^[[:space:]]+/, "")
      gsub(/"/, "", key)
      gsub(/"/, "", $0)
      printf "  %s: %s\n", key, $0
    }
  ' "$manifest"
}

validate_only=0
if [[ ${1:-} == "--validate-only" ]]; then
  validate_only=1
  shift
fi

if [[ $# -lt 1 || $# -gt 2 ]]; then
  usage >&2
  exit 1
fi

source_path=$1
server_dir=${2:-}
temporary_root=""
listing=""

cleanup() {
  [[ -z "$temporary_root" ]] || rm -rf "$temporary_root"
  [[ -z "$listing" ]] || rm -f "$listing"
}
trap cleanup EXIT

if [[ -f "$source_path" ]]; then
  listing=$(mktemp "${TMPDIR:-/tmp}/valheim-world-list.XXXXXX")
  validate_archive "$source_path" "$listing"

  if (( validate_only == 1 )); then
    exit 0
  fi

  echo "Archive: $source_path"
  echo "Archive bytes: $(file_size "$source_path")"
  echo "Archive SHA-256: $(sha256_file "$source_path")"
  echo "Archive entries: $(wc -l < "$listing" | tr -d ' ')"

  temporary_root=$(mktemp -d "${TMPDIR:-/tmp}/valheim-world-inspect.XXXXXX")
  tar -xzf "$source_path" -C "$temporary_root"
  inspect_directory "$temporary_root"
elif [[ -d "$source_path" ]]; then
  (( validate_only == 0 )) || die "--validate-only requires an archive file"
  inspect_directory "$source_path"
else
  die "source does not exist: $source_path"
fi

if [[ -n "$server_dir" ]]; then
  inspect_build "$server_dir"
elif [[ -d /opt/valheim/server ]]; then
  inspect_build /opt/valheim/server
fi
