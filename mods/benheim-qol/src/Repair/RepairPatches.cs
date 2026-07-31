using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Repair;

[HarmonyPatch(typeof(InventoryGui), "OnRepairPressed")]
internal static class GearRepairPatch
{
    private static bool Prefix(InventoryGui __instance)
    {
        if (!InputState.IsShiftHeld())
        {
            return true;
        }

        try
        {
            GearRepair.RepairAll(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Repair-all gear failed; falling back to vanilla repair: {ex.Message}");
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(Player), "Repair")]
internal static class BuildingRepairPatch
{
    private static bool Prefix(Player __instance, ItemDrop.ItemData toolItem)
    {
        if (!InputState.IsShiftHeld())
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
