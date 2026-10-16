#!/usr/bin/env bash
set -euo pipefail

usage() {
  echo "Usage: $0 --check | --install-only | --restart-approved"
  echo 'The last mode explicitly confirms that the current downtime decision is settled.'
}
if [[ $# != 1 ]]; then usage >&2; exit 2; fi
case "$1" in
  --check|--install-only|--restart-approved) mode=$1 ;;
  --help|-h) usage; exit 0 ;;
  *) usage >&2; exit 2 ;;
esac
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"
load_config
root=$(repo_root)
destination=/usr/local/lib/valheim-native-update

if [[ "$mode" != --check ]]; then
  # Installing tooling is separate from approving game downtime. The shared
  # lock also prevents replacing helpers beneath an in-progress update.
  stage=$(remote_ssh 'mktemp -d /tmp/valheim-native-update.XXXXXXXX')
  [[ "$stage" =~ ^/tmp/valheim-native-update\.[A-Za-z0-9]+$ ]] || exit 1
  trap 'remote_ssh "rm -rf $stage" >/dev/null 2>&1 || true' EXIT
  for file in update-valheim-server.py update-valheim-installation backup-valheim-worlds archive-valheim-installation verify-benheim-server-plugins; do
    remote_scp "$root/server/$file" "$stage/$file"
  done
  for file in inspect-world-archive.sh restore-world-archive.sh; do
    remote_scp "$root/scripts/$file" "$stage/$file"
  done
  remote_ssh "flock -n /var/lock/valheim-native-update.lock bash -s -- $stage" <<'REMOTE'
set -euo pipefail
stage=$1
destination=/usr/local/lib/valheim-native-update
install -d -m 0700 "$destination"
for file in update-valheim-server.py archive-valheim-installation verify-benheim-server-plugins inspect-world-archive.sh restore-world-archive.sh; do
  install -m 0700 "$stage/$file" "$destination/$file"
done
install -m 0755 "$stage/update-valheim-installation" /usr/local/bin/valheim-update
install -m 0755 "$stage/backup-valheim-worlds" /usr/local/bin/valheim-backup
REMOTE
fi
if [[ "$mode" == --install-only ]]; then
  echo 'Installed native update tools; no game service action was requested.'
  exit 0
fi
printf -v world_arg '%q' "$VALHEIM_WORLD_NAME"
remote_ssh "python3 $destination/update-valheim-server.py --world $world_arg $mode"
