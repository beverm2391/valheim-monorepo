#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(cd "$root/../.." && pwd)"

python3 "$root/tests/put-away-visibility-checker-test.py"
"$repo_root/tests/server-benheim-support-test.sh"

printf 'Put Away multiplayer lease and visibility evidence checks passed\n'
