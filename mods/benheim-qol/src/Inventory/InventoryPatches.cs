using System;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class InventoryPatches
{
    private static readonly FieldInfo PlayerGridField =
        AccessTools.Field(typeof(InventoryGui), "m_playerGrid");

    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    private static class TogglePocketPatch
    {
        private static bool Prefix(InventoryGrid grid, ItemDrop.ItemData? item)
        {
            bool altHeld = InputState.IsAltHeld();
            Diagnostics.Event(
                "Inventory",
                "item_clicked",
                $"alt={Diagnostics.Bool(altHeld)} item={(item == null ? "none" : item.m_shared.m_name)}");
            return !altHeld || !PocketItemController.TryTogglePlayerItem(grid, item);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    private static class InventoryHotkeysPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (!InventoryVisibility.IsOpen(__instance)
                || InputState.IsTextEntryActive()
                || Player.m_localPlayer == null)
            {
                return;
            }

            try
            {
                if (!InputState.IsAltHeld()
                    && !InputState.IsShiftHeld()
                    && InputState.IsKeyDown(KeyCode.P))
                {
                    InventoryGrid playerGrid = (InventoryGrid)PlayerGridField.GetValue(__instance);
                    ItemDrop.ItemData hoveredItem = playerGrid.GetItem(
                        new Vector2i((int)ZInput.mousePosition.x, (int)ZInput.mousePosition.y));
                    PocketItemController.TryTogglePlayerItem(playerGrid, hoveredItem);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Pocket/quick stack hotkey failed: {ex.Message}");
            }
        }

    }

    [HarmonyPatch(typeof(InventoryGrid), "UpdateInventory")]
    private static class PocketMarkerPatch
    {
        private static void Postfix(InventoryGrid __instance, Inventory inventory)
        {
            PocketMarker.Refresh(__instance, inventory);
        }
    }

}
