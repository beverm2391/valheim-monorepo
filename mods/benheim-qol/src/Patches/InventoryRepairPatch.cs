using System;
using System.Reflection;
using HarmonyLib;

namespace BenheimQoL.Patches;

[HarmonyPatch(typeof(InventoryGui), "OnRepairPressed")]
internal static class InventoryRepairPatch
{
    private const int MaxRepairIterations = 100;

    private static readonly MethodInfo HaveRepairableItemsMethod =
        AccessTools.Method(typeof(InventoryGui), "HaveRepairableItems");

    private static readonly MethodInfo RepairOneItemMethod =
        AccessTools.Method(typeof(InventoryGui), "RepairOneItem");

    private static readonly MethodInfo UpdateRepairMethod =
        AccessTools.Method(typeof(InventoryGui), "UpdateRepair");

    private static readonly MethodInfo UpdateCraftingPanelMethod =
        AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel");

    private static bool Prefix(InventoryGui __instance)
    {
        if (!InputState.IsShiftHeld())
        {
            return true;
        }

        try
        {
            int repaired = 0;
            while (repaired < MaxRepairIterations && HasRepairableItems(__instance))
            {
                RepairOneItemMethod.Invoke(__instance, null);
                repaired++;
            }

            UpdateRepairMethod.Invoke(__instance, null);
            UpdateCraftingPanelMethod.Invoke(__instance, new object[] { false });
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Repair-all gear failed; falling back to vanilla repair: {ex.Message}");
            return true;
        }

        return false;
    }

    private static bool HasRepairableItems(InventoryGui inventoryGui)
    {
        return (bool)(HaveRepairableItemsMethod.Invoke(inventoryGui, null) ?? false);
    }
}
