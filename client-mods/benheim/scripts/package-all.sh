#!/usr/bin/env bash
set -euo pipefail

# The group package is the only distributable package. The coordinator owns
# verification and writes the credential only into ignored, local artifacts.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec "$root/scripts/package-private-test.sh" "$@"
