#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if grep -RqsE 'PortalAutocomplete|PortalTagHistory|Portal tag edit' "$root/src"; then
  printf 'removed portal autocomplete behavior remains in BenheimQoL source\n' >&2
  exit 1
fi

printf 'portal autocomplete removal checks passed\n'
