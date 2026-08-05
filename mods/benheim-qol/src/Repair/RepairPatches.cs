using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Repair;

[HarmonyPatch(typeof(InventoryGui), "OnRepairPressed")]
internal static class GearRepairPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        bool shiftHeld = InputState.IsShiftHeld();
        Diagnostics.Event("Repair", "station_repair_input", $"shift={Diagnostics.Bool(shiftHeld)}");
        if (!shiftHeld)
        {
            return true;
        }

        try
        {
            int repaired = GearRepair.RepairAll(__instance);
            Diagnostics.Event("Repair", "station_repair_all_finished", $"repaired={repaired}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Repair-all gear failed; falling back to vanilla repair: {ex.Message}");
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(Player), "Repair", new[] { typeof(ItemDrop.ItemData), typeof(Piece) })]
internal static class BuildingRepairPatch
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(Player __instance, ItemDrop.ItemData toolItem)
    {
        if (BuildingRepair.IsInvokingNativeRepair)
        {
            return true;
        }

        bool shiftHeld = InputState.IsShiftHeld();
        bool repairMode = __instance.InRepairMode();
        Piece anchor = __instance.GetHoveringPiece();
        Diagnostics.Event(
            "Repair",
            "building_repair_input",
            $"shift={Diagnostics.Bool(shiftHeld)} repair_mode={Diagnostics.Bool(repairMode)} anchor={Diagnostics.Bool(anchor)} target=\"{(anchor ? anchor.gameObject.name : "none")}\"");

        if (!shiftHeld || !repairMode || !anchor)
        {
            return true;
        }

        try
        {
            BuildingRepair.RepairNearby(__instance, toolItem, anchor);
        }
        catch (Exception ex)
        {
            // A batch may have completed earlier native repairs before a later call fails.
            // Do not run the anchor repair again because its costs or effects may already apply.
            Plugin.Log.LogWarning($"Mass building repair stopped after an error: {ex.Message}");
        }

        return false;
    }
}

[HarmonyPatch(typeof(WearNTear), nameof(WearNTear.Repair))]
internal static class BuildingRepairResultPatch
{
    private static void Postfix(WearNTear __instance, bool __result)
    {
        BuildingRepair.RecordNativeRepairResult(__instance, __result);
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.Message), new[]
{
    typeof(MessageHud.MessageType),
    typeof(string),
    typeof(int),
    typeof(Sprite),
})]
internal static class BuildingRepairMessagePatch
{
    private static bool Prefix(MessageHud.MessageType type)
    {
        return !BuildingRepair.IsInvokingNativeRepair || type != MessageHud.MessageType.TopLeft;
    }
}
