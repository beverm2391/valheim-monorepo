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
        CraftingBatchDiagnostics.Reset();
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
        // Preserve Valheim's rebindable AltPlace/gamepad/touch multicraft
        // behavior. The transpiler feeds the native AltPlace result through
        // here so either physical Shift key can request Craft Max.
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
        return InputState.IsShiftHeld();
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

    internal static void RestoreNativeAmount(InventoryGui gui)
    {
        if (trackedGui == gui && nativeMultiCraftAmount > 0)
        {
            gui.m_multiCraftAmount = nativeMultiCraftAmount;
        }
    }
}
