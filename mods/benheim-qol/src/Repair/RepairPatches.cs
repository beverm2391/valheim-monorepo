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
