#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

bash "$root/tests/runtime-test.sh"
bash "$root/tests/install-test.sh"
bash "$root/tests/launcher-test.sh"
node --test "$root/server.test.mjs"
"$root/scripts/build.sh"
