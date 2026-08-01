#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patches="$root/src/Repair/RepairPatches.cs"
repair="$root/src/Repair/BuildingRepair.cs"

grep -Fq 'AccessTools.DeclaredMethod(' "$patches"
grep -Fq 'building_repair_patch_ready' "$patches"
grep -Fq 'building_repair_click_observed' "$repair"
grep -Fq 'RepairRadius = 20f' "$repair"

if grep -RqsE 'PortalAutocomplete|PortalTagHistory|Portal tag edit' "$root/src"; then
  printf 'removed portal autocomplete behavior remains in BenheimQoL source\n' >&2
  exit 1
fi

printf 'repair patch diagnostics and portal removal checks passed\n'
