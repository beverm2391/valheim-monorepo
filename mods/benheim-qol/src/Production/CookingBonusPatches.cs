using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BenheimQoL.Production;

internal static class CookingBonus
{
    internal const float ChanceCeiling = 0.50f;

    internal static float ForCrafting(InventoryGui inventoryGui)
    {
        CraftingStation? station = Player.m_localPlayer?.GetCurrentCraftingStation();
        return station != null && station.m_craftingSkill == Skills.SkillType.Cooking
            ? ChanceCeiling
            : inventoryGui.m_craftBonusChance;
    }

    internal static float ForCookingStation(InventoryGui inventoryGui)
    {
        // Touch the native field so a missing InventoryGui still fails at the
        // same seam instead of changing CookingStation's initialization rules.
        _ = inventoryGui.m_craftBonusChance;
        return ChanceCeiling;
    }
}

[HarmonyPatch(typeof(InventoryGui), "DoCrafting")]
internal static class CookingCraftBonusPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return CookingBonusTranspiler.ReplaceNativeChance(
            instructions,
            AccessTools.Method(typeof(CookingBonus), nameof(CookingBonus.ForCrafting)),
            "InventoryGui.DoCrafting");
    }
}

[HarmonyPatch(typeof(CookingStation), "OnInteract")]
internal static class CookingStationBonusPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        return CookingBonusTranspiler.ReplaceNativeChance(
            instructions,
            AccessTools.Method(typeof(CookingBonus), nameof(CookingBonus.ForCookingStation)),
            "CookingStation.OnInteract");
    }
}

internal static class CookingBonusTranspiler
{
    internal static IEnumerable<CodeInstruction> ReplaceNativeChance(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo? replacement,
        string seam)
    {
        if (replacement == null)
        {
            throw new InvalidOperationException($"Cooking bonus replacement was not found for {seam}.");
        }

        FieldInfo? nativeChance = AccessTools.Field(
            typeof(InventoryGui),
            nameof(InventoryGui.m_craftBonusChance));
        if (nativeChance == null)
        {
            throw new InvalidOperationException("Valheim's native craft bonus chance field was not found.");
        }

        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replaced = 0;
        foreach (CodeInstruction code in codes)
        {
            if (code.opcode != OpCodes.Ldfld || !Equals(code.operand, nativeChance))
            {
                continue;
            }

            // The native InventoryGui instance already on the stack becomes
            // the resolver argument. The roll, skill factor, and +1 amount stay native.
            code.opcode = OpCodes.Call;
            code.operand = replacement;
            replaced++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException(
                $"Expected one native craft bonus chance read in {seam}, found {replaced}.");
        }

        return codes;
    }
}
