#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feedback="$root/src/Inventory/InventoryFeedback.cs"
marker="$root/src/Inventory/PocketMarker.cs"
controller="$root/src/Inventory/PocketItemController.cs"
protection="$root/src/Inventory/PocketItems.cs"
quick_stack="$root/src/Inventory/QuickStack.cs"
quick_stack_feedback="$root/src/Inventory/QuickStackFeedback.cs"
visibility="$root/src/Inventory/InventoryVisibility.cs"

grep -Fq 'AddInworldText' "$feedback"
if grep -Fq '.ShowText(' "$feedback"; then
  printf 'inventory feedback must stay local instead of using broadcast damage text\n' >&2
  exit 1
fi

grep -Fq 'IsAutomaticallyProtected' "$protection"
grep -Fq 'ManualColor' "$marker"
grep -Fq 'AutomaticColor' "$marker"
grep -Fq 'manuallyProtected ? ManualColor : AutomaticColor' "$marker"
grep -Fq 'if (inventoryWasOpen)' "$quick_stack_feedback"
grep -Fq 'QuickStackMessages.AbovePlayerSummary(movedItems)' "$quick_stack_feedback"
grep -Fq 'm_animator' "$visibility"
if grep -Fq 'InventoryFeedback' "$controller"; then
  printf 'pocket toggles must not show floating world text\n' >&2
  exit 1
fi

printf 'inventory protection marker and local-feedback checks passed\n'
