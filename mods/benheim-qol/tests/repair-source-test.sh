#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patches="$root/src/Repair/RepairPatches.cs"
repair="$root/src/Repair/BuildingRepair.cs"

grep -Fq 'internal static class GearRepairPatch' "$patches"
grep -Fq 'station_repair_all_finished' "$patches"
test -e "$repair"
grep -Fq 'BuildingRepair.RepairNearby(__instance, toolItem, anchor)' "$patches"
grep -Fq 'BuildingRepair.IsInvokingNativeRepair' "$patches"
grep -Fq 'BuildingRepair.RecordNativeRepairResult(__instance, __result)' "$patches"
grep -Fq 'Piece anchor = __instance.GetHoveringPiece();' "$patches"
grep -Fq '[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Repair))]' "$patches"
grep -Fq '[HarmonyPatch(typeof(Player), nameof(Player.Message)' "$patches"
grep -Fq 'private const float RepairRadius = 20f' "$repair" || grep -Fq 'internal const float RepairRadius = 20f' "$repair"
grep -Fq 'NativeRepairMethod.Invoke' "$repair"
grep -Fq 'HoveringPieceField.SetValue(player, piece)' "$repair"
grep -Fq 'ReferenceEquals(nativeRepairTarget, repairTarget)' "$repair"
grep -Fq 'toolItem.m_shared.m_useDurability && toolItem.m_durability <= 0f' "$repair"
if grep -Fq 'GetHealthPercentage()' "$repair"; then
  printf 'native WearNTear.Repair must decide authoritative damaged state\n' >&2
  exit 1
fi
grep -Fq 'new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)' "$repair"
grep -Fq 'Localization.instance.Localize(name)' "$repair"
grep -Fq '.OrderByDescending(pair => pair.Value)' "$repair"
grep -Fq 'QuickStackReceiptHud.Show(FormatReceipt(repairedByDisplayName))' "$repair"
grep -Fq '$"Repaired {pair.Value} {Pluralize(pair.Key, pair.Value)}"' "$repair"
grep -Fq 'type != MessageHud.MessageType.TopLeft' "$patches"
if grep -Fq 'WorldFeedback' "$repair"; then
  printf 'mass building repair must use the shared top-left receipt instead of world feedback\n' >&2
  exit 1
fi
if grep -Fq 'wearNTear.Repair()' "$repair"; then
  printf 'mass building repair must invoke Player.Repair instead of mutating pieces directly\n' >&2
  exit 1
fi

printf 'station repair and native-path mass building repair enabled\n'
