using System.Reflection;
using HarmonyLib;

namespace BenheimQoL.Repair;

internal static class GearRepair
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

    internal static void RepairAll(InventoryGui inventoryGui)
    {
        int repaired = 0;
        while (repaired < MaxRepairIterations && HasRepairableItems(inventoryGui))
        {
            RepairOneItemMethod.Invoke(inventoryGui, null);
            repaired++;
        }

        UpdateRepairMethod.Invoke(inventoryGui, null);
        UpdateCraftingPanelMethod.Invoke(inventoryGui, new object[] { false });
    }

    private static bool HasRepairableItems(InventoryGui inventoryGui)
    {
        return (bool)(HaveRepairableItemsMethod.Invoke(inventoryGui, null) ?? false);
    }
}
