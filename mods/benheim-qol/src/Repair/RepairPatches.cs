using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;

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
    private static bool Prefix(Player __instance, ItemDrop.ItemData toolItem, Piece repairPiece)
    {
        bool shiftHeld = InputState.IsShiftHeld();
        bool repairablePiece = repairPiece && repairPiece.m_repairPiece;
        Diagnostics.Event(
            "Repair",
            "building_repair_input",
            $"shift={Diagnostics.Bool(shiftHeld)} repair_piece={Diagnostics.Bool(repairablePiece)} target=\"{(repairPiece ? repairPiece.gameObject.name : "none")}\"");
        if (!repairablePiece || !shiftHeld)
        {
            return true;
        }

        try
        {
            BuildingRepair.RepairNearbyPieces(__instance, toolItem);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Mass building repair failed; falling back to vanilla repair: {ex.Message}");
            return true;
        }

        return false;
    }
}
