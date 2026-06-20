using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Patches;

[HarmonyPatch(typeof(InventoryGui), "ShowSplitDialog")]
internal static class SplitStackPatch
{
    private static readonly FieldInfo SplitInputField =
        AccessTools.Field(typeof(InventoryGui), "m_splitInput");

    private static readonly FieldInfo LastSplitInputField =
        AccessTools.Field(typeof(InventoryGui), "m_lastSplitInput");

    private static readonly FieldInfo SplitNumInputTimeoutSecField =
        AccessTools.Field(typeof(InventoryGui), "m_splitNumInputTimeoutSec");

    private static readonly FieldInfo SplitItemField =
        AccessTools.Field(typeof(InventoryGui), "m_splitItem");

    private static readonly FieldInfo SplitInventoryField =
        AccessTools.Field(typeof(InventoryGui), "m_splitInventory");

    private static readonly FieldInfo CurrentContainerField =
        AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

    private static readonly MethodInfo OnSplitSliderChangedMethod =
        AccessTools.Method(typeof(InventoryGui), "OnSplitSliderChanged");

    private static void Postfix(InventoryGui __instance)
    {
        try
        {
            ClearTypedAmount(__instance);
            SplitNumInputTimeoutSecField.SetValue(__instance, 10f);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not prime split-stack numeric input: {ex.Message}");
        }
    }

    internal static void ClearTypedAmount(InventoryGui inventoryGui)
    {
        SplitInputField.SetValue(inventoryGui, string.Empty);
        LastSplitInputField.SetValue(inventoryGui, DateTime.MinValue);
    }

    internal static void CloseSplitDialog(InventoryGui inventoryGui)
    {
        SplitItemField.SetValue(inventoryGui, null);
        SplitInventoryField.SetValue(inventoryGui, null);
        inventoryGui.m_splitPanel.gameObject.SetActive(false);
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateSplitDialog")]
    private static class DeleteClearsSplitAmountPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            if (!__instance.m_splitSlider.gameObject.activeInHierarchy
                || (!Input.GetKeyDown(KeyCode.Backspace) && !Input.GetKeyDown(KeyCode.Delete)))
            {
                return;
            }

            ClearTypedAmount(__instance);
            __instance.m_splitSlider.value = 1f;
            OnSplitSliderChangedMethod.Invoke(__instance, new object[] { 1f });
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnSplitOk")]
    private static class AutoMoveSplitPatch
    {
        private static bool Prefix(InventoryGui __instance)
        {
            try
            {
                return !TryAutoMoveSplit(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Split auto-transfer failed; falling back to vanilla split: {ex.Message}");
                return true;
            }
        }

        private static bool TryAutoMoveSplit(InventoryGui inventoryGui)
        {
            ItemDrop.ItemData item = (ItemDrop.ItemData)SplitItemField.GetValue(inventoryGui);
            Inventory sourceInventory = (Inventory)SplitInventoryField.GetValue(inventoryGui);
            Container currentContainer = (Container)CurrentContainerField.GetValue(inventoryGui);
            Player player = Player.m_localPlayer;
            if (item == null || sourceInventory == null || !currentContainer || player == null)
            {
                return false;
            }

            Inventory playerInventory = player.GetInventory();
            Inventory containerInventory = currentContainer.GetInventory();
            Inventory targetInventory;
            if (sourceInventory == playerInventory)
            {
                targetInventory = containerInventory;
            }
            else if (sourceInventory == containerInventory)
            {
                targetInventory = playerInventory;
            }
            else
            {
                return false;
            }

            int amount = Mathf.Clamp((int)inventoryGui.m_splitSlider.value, 1, item.m_stack);
            if (!targetInventory.CanAddItem(item, amount))
            {
                return false;
            }

            ItemDrop.ItemData splitItem = item.Clone();
            splitItem.m_stack = amount;
            if (!targetInventory.AddItem(splitItem))
            {
                return false;
            }

            sourceInventory.RemoveItem(item, amount);
            inventoryGui.m_moveItemEffects.Create(inventoryGui.transform.position, Quaternion.identity);
            CloseSplitDialog(inventoryGui);
            return true;
        }
    }
}
