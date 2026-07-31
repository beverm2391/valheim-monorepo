#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<EOF
Usage:
  $0 ARCHIVE
  $0 --local ARCHIVE [DESTINATION] [--expected-sha256 SHA256]

The default mode uploads ARCHIVE and these world-archive tools to the server
from server.env, then replaces /var/lib/valheim/worlds_local there.

--local performs the guarded replacement on this machine. It is used by the
remote wrapper and can also restore a local test directory. DESTINATION defaults
to /var/lib/valheim/worlds_local.
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

restore_local() (
  local archive=$1
  local destination=$2
  local expected_sha=$3
  local parent base canonical_parent listing stage quarantine stamp actual_sha
  local destination_moved=0

  [[ "$destination" == /* ]] || die "destination must be an absolute path: $destination"
  [[ ! -L "$destination" ]] || die "destination must not be a symbolic link: $destination"

  parent=$(dirname "$destination")
  base=$(basename "$destination")
  [[ -d "$parent" ]] || die "destination parent does not exist: $parent"
  canonical_parent=$(cd "$parent" && pwd -P)
  destination="$canonical_parent/$base"
  parent=$canonical_parent
  base=$(basename "$destination")
  [[ "$base" == "worlds_local" ]] || die "destination must resolve to a directory named worlds_local"
  [[ ! -e "$destination" || -d "$destination" ]] || die "destination is not a directory: $destination"

  if command -v systemctl >/dev/null 2>&1 && systemctl is-active --quiet valheim.service; then
    die "valheim.service is active; stop it before restoring"
  fi

  listing=$(mktemp "${TMPDIR:-/tmp}/valheim-world-list.XXXXXX")
  stage=$(mktemp -d "$parent/.${base}.restore.XXXXXX")

  cleanup_local() {
    rm -f "$listing"
    [[ -z "$stage" || ! -d "$stage" ]] || rm -rf "$stage"

    if (( destination_moved == 1 )) && [[ ! -e "$destination" && -d "$quarantine" ]]; then
      echo "Restore failed after quarantine; moving the original directory back." >&2
      mv "$quarantine" "$destination"
    fi
  }
  trap cleanup_local EXIT

  validate_archive "$archive" "$listing"
  if [[ -n "$expected_sha" ]]; then
    actual_sha=$(sha256_file "$archive")
    [[ "$actual_sha" == "$expected_sha" ]] || die "archive SHA-256 mismatch: expected $expected_sha, got $actual_sha"
  fi

  tar -xzf "$archive" -C "$stage"
  if ! find "$stage" -mindepth 1 -print -quit | grep -q .; then
    die "archive extracted an empty world storage directory"
  fi

  if [[ $(id -u) -eq 0 ]] && id -u valheim >/dev/null 2>&1; then
    chown -R valheim:valheim "$stage"
  fi

  quarantine=""
  if [[ -d "$destination" ]]; then
    stamp=$(date -u +%Y%m%dT%H%M%SZ)
    quarantine="${destination}.quarantine-${stamp}"
    [[ ! -e "$quarantine" ]] || die "quarantine path already exists: $quarantine"
    mv "$destination" "$quarantine"
    destination_moved=1
  fi

  mv "$stage" "$destination"
  stage=""
  destination_moved=0
  rm -f "$listing"

  if [[ -n "$quarantine" ]]; then
    echo "Previous world storage quarantined at: $quarantine"
  else
    echo "No previous world storage directory existed."
  fi
  echo "Restored complete world storage at: $destination"
)

if [[ ${1:-} == "--local" ]]; then
  shift
  if [[ $# -lt 1 || $# -gt 4 ]]; then
    usage >&2
    exit 1
  fi

  archive=$1
  shift
  destination=/var/lib/valheim/worlds_local
  expected_sha=""

  if [[ $# -gt 0 && $1 != "--expected-sha256" ]]; then
    destination=$1
    shift
  fi
  if [[ $# -gt 0 ]]; then
    [[ $1 == "--expected-sha256" && $# -eq 2 ]] || die "invalid --local arguments"
    expected_sha=$2
  fi

  restore_local "$archive" "$destination" "$expected_sha"
  exit 0
fi

if [[ $# -ne 1 ]]; then
  usage >&2
  exit 1
fi

archive=$1
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
script_root=$(repo_root)
inspect_script="$script_root/scripts/inspect-world-archive.sh"
restore_script="$script_root/scripts/restore-world-archive.sh"
listing=$(mktemp "${TMPDIR:-/tmp}/valheim-world-list.XXXXXX")
trap 'rm -f "$listing"' EXIT

validate_archive "$archive" "$listing"
archive_sha=$(sha256_file "$archive")
load_config

transfer_id="$(date -u +%Y%m%dT%H%M%SZ)-$$"
remote_archive="/tmp/valheim-world-$transfer_id.tar.gz"
remote_restore="/tmp/valheim-world-restore-$transfer_id.sh"
remote_inspect="/tmp/valheim-world-inspect-$transfer_id.sh"

remote_scp "$archive" "$remote_archive"
remote_scp "$restore_script" "$remote_restore"
remote_scp "$inspect_script" "$remote_inspect"

remote_command="set -euo pipefail
trap 'rm -f $remote_archive $remote_restore $remote_inspect' EXIT
systemctl stop valheim.service
bash $remote_restore --local $remote_archive /var/lib/valheim/worlds_local --expected-sha256 $archive_sha
bash $remote_inspect /var/lib/valheim/worlds_local /opt/valheim/server"
remote_ssh "$remote_command"

echo "Restored archive on $(ssh_target). valheim.service remains stopped."
