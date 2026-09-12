#!/usr/bin/env bash
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

usage() {
  cat <<EOF
Usage:
  $0 WORLD_NAME
  $0 --create-new WORLD_NAME --confirm WORLD_NAME

The normal form only selects a world save that already exists on the server.
Creating a new world requires the explicit form and the name repeated exactly.
EOF
}

die() {
  echo "Error: $*" >&2
  exit 1
}

case "${1:-}" in
  --create-new)
    [[ $# -eq 4 && $3 == --confirm ]] || { usage >&2; exit 1; }
    action=create
    world=$2
    confirmation=$4
    [[ "$confirmation" == "$world" ]] || die "--confirm must exactly repeat WORLD_NAME"
    ;;
  "")
    usage >&2
    exit 1
    ;;
  *)
    [[ $# -eq 1 ]] || { usage >&2; exit 1; }
    action=switch
    world=$1
    confirmation=""
    ;;
esac

if [[ ! "$world" =~ ^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$ ]]; then
  die "WORLD_NAME must be 1-64 letters, numbers, dashes, or underscores, and must start with a letter or number"
fi

load_config
root=$(repo_root)
env_file=${VALHEIM_ENV_FILE:-"$root/server.env"}
[[ $(grep -c '^VALHEIM_WORLD_NAME=' "$env_file" || true) -eq 1 ]] || \
  die "$env_file must contain exactly one VALHEIM_WORLD_NAME assignment"

local_next=$(mktemp "$(dirname "$env_file")/.server.env.world.XXXXXX")
remote_helper="/tmp/valheim-switch-world-$(date -u +%Y%m%dT%H%M%SZ)-$$"
remote_created=0
cleanup() {
  local status=$?
  rm -f "$local_next"
  if (( remote_created == 1 )); then
    remote_ssh "rm -f $remote_helper" >/dev/null 2>&1 || true
  fi
  exit "$status"
}
trap cleanup EXIT

cp -p "$env_file" "$local_next"
awk -v world="$world" '
  /^VALHEIM_WORLD_NAME=/ { print "VALHEIM_WORLD_NAME=" world; next }
  { print }
' "$env_file" > "$local_next"

remote_scp "$root/server/switch-valheim-world" "$remote_helper"
remote_created=1
if [[ "$action" == create ]]; then
  remote_ssh "bash $remote_helper create $world $confirmation"
else
  remote_ssh "bash $remote_helper switch $world"
fi
remote_ssh "rm -f $remote_helper"
remote_created=0

mv "$local_next" "$env_file"
echo "Updated $env_file so future configuration deployments keep world '$world'."
