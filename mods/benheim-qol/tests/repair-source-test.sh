#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patches="$root/src/Repair/RepairPatches.cs"
repair="$root/src/Repair/BuildingRepair.cs"

grep -Fq 'internal static class GearRepairPatch' "$patches"
grep -Fq 'station_repair_all_finished' "$patches"
test ! -e "$repair"
if grep -Fq 'BuildingRepair' "$patches"; then
  printf 'mass building repair must stay disabled until target detection is proven\n' >&2
  exit 1
fi

printf 'station repair enabled and mass building repair disabled\n'
