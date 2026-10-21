#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
legacy_leaf="qol"
retired_path="mods/benheim-${legacy_leaf}"

# The package verifier runs on a committed source export without a .git
# directory. Search its source directly, still excluding ignored build output.
if rg --hidden --no-require-git --glob '!.git' -nF -- "$retired_path" "$repo_root"; then
  echo "retired client-mod path remains in tracked sources" >&2
  exit 1
else
  search_status=$?
  if [[ "$search_status" != 1 ]]; then
    exit "$search_status"
  fi
fi

test -d "$repo_root/client-mods/benheim"
test ! -e "$repo_root/$retired_path"

echo "repository layout test passed"
