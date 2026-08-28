#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
legacy_leaf="qol"
retired_path="mods/benheim-${legacy_leaf}"

if git -C "$repo_root" grep -nF -- "$retired_path"; then
  echo "retired client-mod path remains in tracked sources" >&2
  exit 1
fi

test -d "$repo_root/client-mods/benheim"
test ! -e "$repo_root/$retired_path"

echo "repository layout test passed"
