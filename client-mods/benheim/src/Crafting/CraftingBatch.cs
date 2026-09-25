using System;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BenheimQoL.Crafting;

internal static class CraftingBatch
{
    private static readonly FieldInfo? SelectedRecipeField =
        AccessTools.Field(typeof(InventoryGui), "m_selectedRecipe");
    private static readonly PropertyInfo? SelectedRecipeProperty =
        SelectedRecipeField?.FieldType.GetProperty("Recipe");
    private static readonly PropertyInfo? SelectedItemProperty =
        SelectedRecipeField?.FieldType.GetProperty("ItemData");

    private static InventoryGui? trackedGui;
    private static int nativeMultiCraftAmount;
    private static ActiveCraft? activeCraft;
    private static int maximumFrame = -1;
    private static Player? maximumPlayer;
    private static Recipe? maximumRecipe;
    private static int maximumValue;

    internal sealed class CraftStartState
    {
        internal bool MaxRequested { get; set; }
        internal bool Suppressed { get; set; }
        internal int RequestedCrafts { get; set; }
    }

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

    internal static void Initialize(InventoryGui gui)
    {
        if (SelectedRecipeField == null
            || SelectedRecipeProperty == null
            || SelectedItemProperty == null)
        {
            throw new MissingMemberException(
                "Craft Max requires InventoryGui's selected-recipe contract.");
        }

        trackedGui = gui;
        nativeMultiCraftAmount = Math.Max(1, gui.m_multiCraftAmount);
        activeCraft = null;
        maximumFrame = -1;
    }

    internal static void PrepareRecipeFrame(InventoryGui gui, float craftTimer)
    {
        // Valheim applies the same skill factor after choosing one of these two
        // base durations. Keeping the bases equal therefore makes every native
        // batch take exactly the time of one craft without replacing its timer.
        gui.m_multiCraftDuration = gui.m_craftDuration;

        if (craftTimer >= 0f || trackedGui != gui)
        {
            return;
        }

        if (IsCraftMaxRequested()
            && TryGetSelectedCraft(gui, out Recipe recipe)
            && Player.m_localPlayer != null)
        {
            int maximum = MaximumCraftsThisFrame(Player.m_localPlayer, recipe);
            gui.m_multiCraftAmount = Math.Max(1, maximum);
            return;
        }

        RestoreNativeAmount(gui);
    }

    internal static void PresentCraftMax(InventoryGui gui, float craftTimer)
    {
        if (craftTimer >= 0f
            || !IsCraftMaxRequested()
            || !TryGetSelectedCraft(gui, out Recipe recipe)
            || Player.m_localPlayer == null)
        {
            return;
        }

        int maximum = MaximumCraftsThisFrame(Player.m_localPlayer, recipe);
        TMP_Text? label = gui.m_craftButton?.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = maximum > 0 ? $"Craft Max x {maximum}" : "Craft Max";
        }
    }

    internal static bool PrepareCraftPress(
        InventoryGui gui,
        CraftStartState state)
    {
        state.MaxRequested = IsCraftMaxRequested();
        if (!state.MaxRequested
            || !TryGetSelectedCraft(gui, out Recipe recipe)
            || Player.m_localPlayer == null)
        {
            return true;
        }

        // Recompute on the click. A presentation cache must never let a
        // just-changed inventory authorize more output than the resources
        // currently allow.
        int maximum = MaximumCrafts(Player.m_localPlayer, recipe);
        state.RequestedCrafts = maximum;
        if (maximum > 0)
        {
            gui.m_multiCraftAmount = maximum;
            return true;
        }

        state.Suppressed = true;
        Diagnostics.Emit(
            DiagnosticEvent.Create("Crafting", "craft_batch_rejected")
                .String("result", "nothing_allowed")
                .String("recipe", recipe.m_item?.m_itemData?.m_shared?.m_name ?? "unknown")
                .Boolean("craft_max", true));
        return false;
    }

    internal static void ObserveCraftStarted(
        InventoryGui gui,
        CraftStartState state,
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

        Player player = Player.m_localPlayer;
        Inventory inventory = player.GetInventory();
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

    internal static void ObserveCraftFinished(InventoryGui gui, Recipe? recipe)
    {
        ActiveCraft? craft = activeCraft;
        if (craft == null || craft.Gui != gui || recipe != craft.Recipe)
        {
            return;
        }

        int outputAfter = craft.Inventory.CountItems(craft.ItemName);
        int outputAdded = Math.Max(0, outputAfter - craft.OutputBefore);
        long durationMs = (long)Math.Max(
            0f,
            (Time.realtimeSinceStartup - craft.StartedAt) * 1000f);
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
        activeCraft = null;
        RestoreNativeAmount(gui);
    }

    internal static void ObserveCraftFailed(InventoryGui gui, Recipe? recipe, Exception exception)
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
                .Integer(
                    "duration_ms",
                    (long)Math.Max(0f, (Time.realtimeSinceStartup - craft.StartedAt) * 1000f))
                .Boolean("craft_max", craft.CraftMax));
        activeCraft = null;
        RestoreNativeAmount(gui);
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
                    .Integer(
                        "duration_ms",
                        (long)Math.Max(0f, (Time.realtimeSinceStartup - craft.StartedAt) * 1000f))
                    .Boolean("craft_max", craft.CraftMax));
            activeCraft = null;
        }
        RestoreNativeAmount(gui);
    }

    internal static int MaximumCrafts(Player player, Recipe recipe)
    {
        if (recipe.m_item == null)
        {
            return 0;
        }

        Inventory inventory = player.GetInventory();
        ItemDrop.ItemData output = recipe.m_item.m_itemData;
        int outputCapacity = inventory.FindFreeStackSpace(
                output.m_shared.m_name,
                output.m_worldLevel)
            + inventory.GetEmptySlots() * output.m_shared.m_maxStackSize;
        int capacityUpperBound = outputCapacity / Math.Max(1, recipe.m_amount);

        Func<int, bool> isAllowed =
            count => IsAllowed(player, inventory, recipe, count);
        if (!recipe.m_requireOnlyOneIngredient)
        {
            return CraftingBatchRules.FindMaximumCrafts(
                capacityUpperBound,
                isAllowed);
        }

        // GetAmount selects one ingredient stack. Selection availability is
        // monotonic even though the selected quality (and therefore output)
        // is not, so use it to avoid scanning empty inventory capacity.
        int selectionUpperBound = CraftingBatchRules.FindMaximumCrafts(
            capacityUpperBound,
            count => CanSelectSingleIngredient(recipe, count));
        return CraftingBatchRules.FindMaximumCraftsDescending(
            selectionUpperBound,
            isAllowed);
    }

    private static bool IsAllowed(
        Player player,
        Inventory inventory,
        Recipe recipe,
        int craftCount)
    {
        CraftingStation? requiredStation = recipe.GetRequiredStation(1);
        CraftingStation? currentStation = player.GetCurrentCraftingStation();
        bool stationUsable = requiredStation == null
            || (currentStation != null && currentStation.CheckUsable(player, showMessage: false));
        bool noCostCheat = player.NoCostCheat();
        bool worldNoCost = ZoneSystem.instance != null
            && ZoneSystem.instance.GetGlobalKey(GlobalKeys.NoCraftCost);
        bool requirementsMet = player.HaveRequirements(
            recipe,
            discover: false,
            qualityLevel: 1,
            amount: craftCount);
        if (!(noCostCheat || (stationUsable && (requirementsMet || worldNoCost))))
        {
            return false;
        }

        // GetAmount owns one-ingredient quality output and the exact input
        // selection Valheim will later consume. Ask it only after requirements
        // pass because the native method expects a selected ingredient.
        int outputAmount;
        try
        {
            outputAmount = recipe.GetAmount(
                1,
                out _,
                out ItemDrop.ItemData _,
                craftCount);
        }
        catch (NullReferenceException) when (recipe.m_requireOnlyOneIngredient)
        {
            return false;
        }
        return inventory.CanAddItem(recipe.m_item.gameObject, outputAmount);
    }

    private static bool CanSelectSingleIngredient(Recipe recipe, int craftCount)
    {
        try
        {
            recipe.GetAmount(
                1,
                out _,
                out ItemDrop.ItemData _,
                craftCount);
            return true;
        }
        catch (NullReferenceException)
        {
            return false;
        }
    }

    internal static bool ResolveNativeBatchModifier(bool nativeRequested)
    {
        // Preserve Valheim's existing Shift/gamepad/touch multicraft behavior.
        // The transpiler feeds the native AltPlace (Left Shift) result through
        // here so Option/Alt can join the same execution path.
        return nativeRequested || IsCraftMaxRequested();
    }

    private static int MaximumCraftsThisFrame(Player player, Recipe recipe)
    {
        if (maximumFrame == Time.frameCount
            && maximumPlayer == player
            && maximumRecipe == recipe)
        {
            return maximumValue;
        }

        maximumFrame = Time.frameCount;
        maximumPlayer = player;
        maximumRecipe = recipe;
        maximumValue = MaximumCrafts(player, recipe);
        return maximumValue;
    }

    private static bool IsCraftMaxRequested()
    {
        // Alt is named Option on Mac. This is deliberately the physical Alt
        // modifier: Valheim's logical AltPlace action defaults to Left Shift.
        return InputState.IsAltHeld();
    }

    private static bool TryGetSelectedCraft(InventoryGui gui, out Recipe recipe)
    {
        recipe = null!;
        if (!gui.InCraftTab()
            || SelectedRecipeField == null
            || SelectedRecipeProperty == null
            || SelectedItemProperty == null)
        {
            return false;
        }

        object? pair = SelectedRecipeField.GetValue(gui);
        Recipe? selected = pair == null ? null : SelectedRecipeProperty.GetValue(pair) as Recipe;
        ItemDrop.ItemData? upgrade = pair == null
            ? null
            : SelectedItemProperty.GetValue(pair) as ItemDrop.ItemData;
        if (selected == null || upgrade != null)
        {
            return false;
        }
        recipe = selected;
        return true;
    }

    private static void RestoreNativeAmount(InventoryGui gui)
    {
        if (trackedGui == gui && nativeMultiCraftAmount > 0)
        {
            gui.m_multiCraftAmount = nativeMultiCraftAmount;
        }
    }
}
