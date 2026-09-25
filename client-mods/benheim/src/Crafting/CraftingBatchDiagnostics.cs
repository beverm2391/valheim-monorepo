using System;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Crafting;

internal static class CraftingBatchDiagnostics
{
    private static ActiveCraft? activeCraft;

    private sealed class ActiveCraft
    {
        internal string OperationId { get; set; } = string.Empty;
        internal InventoryGui Gui { get; set; } = null!;
        internal Inventory Inventory { get; set; } = null!;
        internal Recipe Recipe { get; set; } = null!;
        internal string ItemName { get; set; } = string.Empty;
        internal int RequestedCrafts { get; set; }
        internal int OutputBefore { get; set; }
        internal float StartedAt { get; set; }
        internal float NativeSingleDuration { get; set; }
        internal bool CraftMax { get; set; }
    }

    internal static void Reset()
    {
        activeCraft = null;
    }

    internal static void ObserveStarted(
        InventoryGui gui,
        CraftingBatch.CraftStartState state,
        float craftTimer,
        bool multiCrafting,
        Recipe? craftRecipe,
        ItemDrop.ItemData? upgradeItem)
    {
        if (state.Suppressed
            || craftTimer < 0f
            || craftRecipe == null
            || upgradeItem != null
            || Player.m_localPlayer == null)
        {
            return;
        }

        Inventory inventory = Player.m_localPlayer.GetInventory();
        string itemName = craftRecipe.m_item.m_itemData.m_shared.m_name;
        int requestedCrafts = multiCrafting ? gui.m_multiCraftAmount : 1;
        string operationId = Diagnostics.NewOperationId();
        activeCraft = new ActiveCraft
        {
            OperationId = operationId,
            Gui = gui,
            Inventory = inventory,
            Recipe = craftRecipe,
            ItemName = itemName,
            RequestedCrafts = requestedCrafts,
            OutputBefore = inventory.CountItems(itemName),
            StartedAt = Time.realtimeSinceStartup,
            NativeSingleDuration = gui.m_craftDuration,
            CraftMax = state.MaxRequested,
        };

        Diagnostics.Emit(
            DiagnosticEvent.Create("Crafting", "craft_batch_started")
                .String("operation_id", operationId)
                .String("recipe", itemName)
                .Integer("requested_crafts", requestedCrafts)
                .Integer("expected_base_output", craftRecipe.m_amount * requestedCrafts)
                .Number("native_single_duration_seconds", gui.m_craftDuration)
                .Number("batch_duration_seconds", gui.m_multiCraftDuration)
                .Boolean("multi_craft", multiCrafting)
                .Boolean("craft_max", state.MaxRequested));
    }

    internal static void ObserveFinished(InventoryGui gui, Recipe? recipe)
    {
        ActiveCraft? craft = activeCraft;
        if (craft == null || craft.Gui != gui || recipe != craft.Recipe)
        {
            return;
        }

        int outputAfter = craft.Inventory.CountItems(craft.ItemName);
        int outputAdded = Math.Max(0, outputAfter - craft.OutputBefore);
        long durationMs = ElapsedMilliseconds(craft);
        Diagnostics.Emit(
            DiagnosticEvent.Create("Crafting", "craft_batch_finished")
                .String("operation_id", craft.OperationId)
                .String("recipe", craft.ItemName)
                .String("result", outputAdded > 0 ? "crafted" : "rejected")
                .Integer("requested_crafts", craft.RequestedCrafts)
                .Integer("output_added", outputAdded)
                .Integer("duration_ms", durationMs)
                .Number("native_single_duration_seconds", craft.NativeSingleDuration)
                .Boolean("craft_max", craft.CraftMax));
        Complete(gui);
    }

    internal static void ObserveFailed(
        InventoryGui gui,
        Recipe? recipe,
        Exception exception)
    {
        ActiveCraft? craft = activeCraft;
        if (craft == null || craft.Gui != gui || recipe != craft.Recipe)
        {
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("Crafting", "craft_batch_finished")
                .String("operation_id", craft.OperationId)
                .String("recipe", craft.ItemName)
                .String("result", "error")
                .String("reason", Diagnostics.Flatten(exception.Message))
                .Integer("requested_crafts", craft.RequestedCrafts)
                .Integer("output_added", 0)
                .Integer("duration_ms", ElapsedMilliseconds(craft))
                .Boolean("craft_max", craft.CraftMax));
        Complete(gui);
    }

    internal static void Cancel(InventoryGui gui, string reason)
    {
        ActiveCraft? craft = activeCraft;
        if (craft != null && craft.Gui == gui)
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("Crafting", "craft_batch_finished")
                    .String("operation_id", craft.OperationId)
                    .String("recipe", craft.ItemName)
                    .String("result", "canceled")
                    .String("reason", reason)
                    .Integer("requested_crafts", craft.RequestedCrafts)
                    .Integer("output_added", 0)
                    .Integer("duration_ms", ElapsedMilliseconds(craft))
                    .Boolean("craft_max", craft.CraftMax));
        }
        Complete(gui);
    }

    private static long ElapsedMilliseconds(ActiveCraft craft)
    {
        return (long)Math.Max(
            0f,
            (Time.realtimeSinceStartup - craft.StartedAt) * 1000f);
    }

    private static void Complete(InventoryGui gui)
    {
        activeCraft = null;
        CraftingBatch.RestoreNativeAmount(gui);
    }
}
