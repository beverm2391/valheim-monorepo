#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(cd "$root/../.." && pwd)"

"$repo_root/tests/server-benheim-support-test.sh"

printf 'Put Away multiplayer lease, authority, and conservation checks passed\n'
