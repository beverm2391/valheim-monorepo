using System;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class SplitStackController
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

    internal static void PrimeNumericInput(InventoryGui inventoryGui)
    {
        ClearTypedAmount(inventoryGui);
        SplitNumInputTimeoutSecField.SetValue(inventoryGui, 10f);
        Diagnostics.Event("Inventory", "split_opened", "numeric_input=true timeout_seconds=10");
    }

    internal static void ClearAmount(InventoryGui inventoryGui)
    {
        ClearTypedAmount(inventoryGui);
        inventoryGui.m_splitSlider.value = 1f;
        OnSplitSliderChangedMethod.Invoke(inventoryGui, new object[] { 1f });
        Diagnostics.Event("Inventory", "split_amount_reset", "amount=1");
    }

    internal static bool TryAutoMove(InventoryGui inventoryGui)
    {
        ItemDrop.ItemData item = (ItemDrop.ItemData)SplitItemField.GetValue(inventoryGui);
        Inventory sourceInventory = (Inventory)SplitInventoryField.GetValue(inventoryGui);
        Container currentContainer = (Container)CurrentContainerField.GetValue(inventoryGui);
        Player player = Player.m_localPlayer;
        if (item == null || sourceInventory == null || !currentContainer || player == null)
        {
            Diagnostics.Event("Inventory", "split_auto_move_skipped", "reason=no_open_container");
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
            Diagnostics.Event("Inventory", "split_auto_move_skipped", "reason=unknown_source_inventory");
            return false;
        }

        int amount = Mathf.Clamp((int)inventoryGui.m_splitSlider.value, 1, item.m_stack);
        if (!targetInventory.CanAddItem(item, amount))
        {
            Diagnostics.Event("Inventory", "split_auto_move_skipped", $"reason=target_full amount={amount}");
            return false;
        }

        ItemDrop.ItemData splitItem = item.Clone();
        splitItem.m_stack = amount;
        if (!targetInventory.AddItem(splitItem))
        {
            Diagnostics.Event("Inventory", "split_auto_move_skipped", $"reason=add_failed amount={amount}");
            return false;
        }

        sourceInventory.RemoveItem(item, amount);
        inventoryGui.m_moveItemEffects.Create(inventoryGui.transform.position, Quaternion.identity);
        CloseDialog(inventoryGui);
        Diagnostics.Event(
            "Inventory",
            "split_auto_moved",
            $"item={item.m_shared.m_name} amount={amount} direction={(sourceInventory == playerInventory ? "to_container" : "to_player")}");
        return true;
    }

    private static void ClearTypedAmount(InventoryGui inventoryGui)
    {
        SplitInputField.SetValue(inventoryGui, string.Empty);
        LastSplitInputField.SetValue(inventoryGui, DateTime.MinValue);
    }

    private static void CloseDialog(InventoryGui inventoryGui)
    {
        SplitItemField.SetValue(inventoryGui, null);
        SplitInventoryField.SetValue(inventoryGui, null);
        inventoryGui.m_splitPanel.gameObject.SetActive(false);
    }
}
