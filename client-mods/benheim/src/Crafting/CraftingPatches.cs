using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BenheimQoL.Crafting;

[HarmonyPatch(typeof(InventoryGui), "Awake")]
internal static class CraftingBatchAwakePatch
{
    [HarmonyPostfix]
    private static void Postfix(InventoryGui __instance)
    {
        CraftingBatch.Initialize(__instance);
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
internal static class CraftingBatchRecipePatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return CraftingBatchModifierTranspiler.ExtendAltPlace(instructions);
    }

    [HarmonyPrefix]
    private static void Prefix(InventoryGui __instance, float ___m_craftTimer)
    {
        CraftingBatch.PrepareRecipeFrame(__instance, ___m_craftTimer);
    }

    [HarmonyPostfix]
    private static void Postfix(InventoryGui __instance, float ___m_craftTimer)
    {
        CraftingBatch.PresentCraftMax(__instance, ___m_craftTimer);
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
internal static class CraftingBatchPressedPatch
{
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return CraftingBatchModifierTranspiler.ExtendAltPlace(instructions);
    }

    [HarmonyPrefix]
    private static bool Prefix(
        InventoryGui __instance,
        out CraftingBatch.CraftStartState __state)
    {
        __state = new CraftingBatch.CraftStartState();
        return CraftingBatch.PrepareCraftPress(__instance, __state);
    }

    [HarmonyPostfix]
    private static void Postfix(
        InventoryGui __instance,
        CraftingBatch.CraftStartState __state,
        float ___m_craftTimer,
        bool ___m_multiCrafting,
        Recipe? ___m_craftRecipe,
        ItemDrop.ItemData? ___m_craftUpgradeItem)
    {
        CraftingBatch.ObserveCraftStarted(
            __instance,
            __state,
            ___m_craftTimer,
            ___m_multiCrafting,
            ___m_craftRecipe,
            ___m_craftUpgradeItem);
    }
}

internal static class CraftingBatchModifierTranspiler
{
    private static readonly MethodInfo NativeGetButton = AccessTools.Method(
        typeof(ZInput),
        "GetButton",
        new[] { typeof(string) });
    private static readonly MethodInfo ResolveBatchModifier = AccessTools.Method(
        typeof(CraftingBatch),
        nameof(CraftingBatch.ResolveNativeBatchModifier));

    internal static IEnumerable<CodeInstruction> ExtendAltPlace(
        IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> source = new(instructions);
        bool patched = false;
        for (int index = 0; index < source.Count; index++)
        {
            CodeInstruction instruction = source[index];
            yield return instruction;
            if (index == 0
                || source[index - 1].opcode != OpCodes.Ldstr
                || !Equals(source[index - 1].operand, "AltPlace")
                || !instruction.Calls(NativeGetButton))
            {
                continue;
            }

            patched = true;
            yield return new CodeInstruction(OpCodes.Call, ResolveBatchModifier);
        }

        if (!patched)
        {
            throw new MissingMethodException(
                "Craft Max could not find InventoryGui's AltPlace input seam.");
        }
    }
}

[HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
internal static class CraftingBatchFinishedPatch
{
    [HarmonyPostfix]
    private static void Postfix(InventoryGui __instance, Recipe? ___m_craftRecipe)
    {
        CraftingBatch.ObserveCraftFinished(__instance, ___m_craftRecipe);
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(
        InventoryGui __instance,
        Recipe? ___m_craftRecipe,
        Exception? __exception)
    {
        if (__exception != null)
        {
            CraftingBatch.ObserveCraftFailed(__instance, ___m_craftRecipe, __exception);
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnCraftCancelPressed")]
internal static class CraftingBatchCancelPatch
{
    [HarmonyPrefix]
    private static void Prefix(InventoryGui __instance)
    {
        CraftingBatch.Cancel(__instance, "player_cancel");
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
internal static class CraftingBatchHidePatch
{
    [HarmonyPrefix]
    private static void Prefix(InventoryGui __instance)
    {
        CraftingBatch.Cancel(__instance, "inventory_hidden");
    }
}
