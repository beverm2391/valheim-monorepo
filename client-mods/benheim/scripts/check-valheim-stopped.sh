#!/usr/bin/env bash
set -euo pipefail

quiet=0
if [[ "${1:-}" == "--quiet" ]]; then
  quiet=1
  shift
fi
if [[ "$#" -ne 0 ]]; then
  echo "Usage: $0 [--quiet]" >&2
  exit 64
fi

# Match only executable identities. Command-line matching can match the
# operator's own shell or safe wrapper when its arguments mention Valheim.
running=()
for executable in Valheim valheim valheim.x86_64; do
  pids="$(pgrep -x "$executable" 2>/dev/null || true)"
  if [[ -n "$pids" ]]; then
    running+=("$executable:$pids")
  fi
done

if [[ "${#running[@]}" -ne 0 ]]; then
  if [[ "$quiet" != "1" ]]; then
    printf 'Valheim is running (%s). Quit it completely before continuing.\n' \
      "$(IFS=', '; echo "${running[*]}")" >&2
  fi
  exit 1
fi

if [[ "$quiet" != "1" ]]; then
  echo "Valheim is not running."
fi
