#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patches="$root/src/Repair/RepairPatches.cs"
repair="$root/src/Repair/BuildingRepair.cs"

grep -Fq 'AccessTools.DeclaredMethod(' "$patches"
grep -Fq 'building_repair_patch_ready' "$patches"
grep -Fq 'building_repair_click_observed' "$repair"
grep -Fq 'RepairRadius = 20f' "$repair"
grep -Fq 'if (repaired > 1)' "$repair"
grep -Fq 'WorldFeedback.ShowAt(GetFeedbackPosition(anchor)' "$repair"
grep -Fq 'pieces repaired' "$repair"

printf 'repair patch diagnostic checks passed\n'
