using System;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class InventoryPatches
{
    private static readonly FieldInfo CurrentContainerField =
        AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

    private static readonly FieldInfo PlayerGridField =
        AccessTools.Field(typeof(InventoryGui), "m_playerGrid");

    private static readonly FieldInfo AnimatorField =
        AccessTools.Field(typeof(InventoryGui), "m_animator");

    [HarmonyPatch(typeof(InventoryGui), "OnSelectedItem")]
    private static class TogglePocketPatch
    {
        private static bool Prefix(InventoryGrid grid, ItemDrop.ItemData item)
        {
            return !InputState.IsAltHeld() || !PocketItemController.TryTogglePlayerItem(grid, item);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "Update")]
    private static class InventoryHotkeysPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (!IsInventoryVisible(__instance)
                || TextInput.IsVisible()
                || Console.IsVisible()
                || Player.m_localPlayer == null)
            {
                return;
            }

            try
            {
                if (InputState.IsAltHeld() && InputState.IsKeyDown(KeyCode.P))
                {
                    Container? currentContainer = (Container?)CurrentContainerField.GetValue(__instance);
                    QuickStack.Run(Player.m_localPlayer, __instance, currentContainer);
                    return;
                }

                if (InputState.IsKeyDown(KeyCode.P))
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

        private static bool IsInventoryVisible(InventoryGui inventoryGui)
        {
            Animator? animator = (Animator?)AnimatorField.GetValue(inventoryGui);
            return animator != null && animator.GetBool("visible");
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
