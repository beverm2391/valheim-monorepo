#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feedback="$root/src/Inventory/InventoryFeedback.cs"
marker="$root/src/Inventory/PocketMarker.cs"
protection="$root/src/Inventory/PocketItems.cs"

grep -Fq 'AddInworldText' "$feedback"
if grep -Fq '.ShowText(' "$feedback"; then
  printf 'inventory feedback must stay local instead of using broadcast damage text\n' >&2
  exit 1
fi

grep -Fq 'IsAutomaticallyProtected' "$protection"
grep -Fq 'ManualColor' "$marker"
grep -Fq 'AutomaticColor' "$marker"
grep -Fq 'manuallyProtected ? ManualColor : AutomaticColor' "$marker"

printf 'inventory protection marker and local-feedback checks passed\n'
