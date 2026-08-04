#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fill="$root/src/Production/StationFill.cs"
patches="$root/src/Production/StationFillPatches.cs"
overlay="$root/src/Shortcuts/ShortcutOverlayContent.cs"

grep -Fq 'InputState.IsShiftHeld()' "$fill"
grep -Fq 'StateUpdateTimeoutSeconds = 1f' "$fill"
grep -Fq 'CreateAddOne' "$fill"
grep -Fq 'user.GetInventory().GetItem(selectedItemName)' "$fill"
grep -Fq 'station_fill_started' "$fill"
grep -Fq 'station_fill_finished' "$fill"
grep -Fq 'result = "state_update_timeout"' "$fill"
grep -Fq 'Filled {added} items' "$fill"

grep -Fq 'HarmonyPatch(typeof(Smelter), "OnAddOre")' "$patches"
grep -Fq 'HarmonyPatch(typeof(Smelter), "OnAddFuel")' "$patches"
grep -Fq 'HarmonyPatch(typeof(CookingStation), "OnAddFoodSwitch")' "$patches"
grep -Fq 'HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")' "$patches"
grep -Fq 'StationFill.IsInvokingVanilla' "$patches"
grep -Fq 'Left Shift + station input' "$overlay"

printf 'station fill source checks passed\n'
