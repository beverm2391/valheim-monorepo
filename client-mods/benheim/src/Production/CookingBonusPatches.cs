using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Production;

internal static class CookingBonus
{
    internal const float ChanceCeiling = 0.50f;
    private static bool nonCookingGuardReported;

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

    internal static bool RollForCrafting(
        float nativeRoll,
        float nativeSkillFactor,
        float configuredBaseChance,
        int nativeBonusCountBefore)
    {
        CraftingStation? station = Player.m_localPlayer?.GetCurrentCraftingStation();
        Skills.SkillType skill = station?.m_craftingSkill ?? Skills.SkillType.None;
        bool cookingGate = skill == Skills.SkillType.Cooking;
        float effectiveChance = nativeSkillFactor * configuredBaseChance;
        bool succeeded = nativeRoll < effectiveChance;
        int configuredBonusAmount = InventoryGui.instance.m_craftBonusAmount;
        int bonusCount = succeeded ? configuredBonusAmount : 0;
        int nativeBonusCountAfter = nativeBonusCountBefore + bonusCount;
        int nativeResultIncrement = succeeded ? nativeBonusCountAfter : 0;
        bool report = cookingGate || !nonCookingGuardReported;
        if (!cookingGate)
        {
            nonCookingGuardReported = true;
        }

        if (report)
        {
            Diagnostics.Emit(
                CreateNativeRollEvent(
                    "InventoryGui.DoCrafting",
                    "crafted_item_bonus",
                    skill,
                    cookingGate,
                    nativeRoll,
                    nativeSkillFactor,
                    configuredBaseChance,
                    effectiveChance,
                    succeeded,
                    configuredBonusAmount,
                    bonusCount)
                    .Integer("native_bonus_count_before", nativeBonusCountBefore)
                    .Integer("native_bonus_count_after", nativeBonusCountAfter)
                    .Integer("native_result_increment", nativeResultIncrement));
        }
        return succeeded;
    }

    internal static bool RollForCookingStation(
        float nativeRoll,
        float nativeSkillFactor,
        float configuredBaseChance,
        int nativeResultCountBefore)
    {
        float effectiveChance = nativeSkillFactor * configuredBaseChance;
        bool succeeded = nativeRoll < effectiveChance;
        int configuredBonusAmount = InventoryGui.instance.m_craftBonusAmount;
        int bonusCount = succeeded ? configuredBonusAmount : 0;
        int nativeResultCount = nativeResultCountBefore + bonusCount;
        Diagnostics.Emit(
            CreateNativeRollEvent(
                "CookingStation.OnInteract",
                "completed_food_retrieval",
                Skills.SkillType.Cooking,
                true,
                nativeRoll,
                nativeSkillFactor,
                configuredBaseChance,
                effectiveChance,
                succeeded,
                configuredBonusAmount,
                bonusCount)
                .Integer("native_result_count_before", nativeResultCountBefore)
                .Integer("native_result_count", nativeResultCount));
        return succeeded;
    }

    private static DiagnosticEvent CreateNativeRollEvent(
        string source,
        string path,
        Skills.SkillType skill,
        bool cookingGate,
        float nativeRoll,
        float nativeSkillFactor,
        float configuredBaseChance,
        float effectiveChance,
        bool succeeded,
        int configuredBonusAmount,
        int bonusCount)
    {
        // The transpiler passes Valheim's already-consumed random value here.
        // The observer returns the equivalent native comparison without
        // drawing another random value or moving the draw.
        return DiagnosticEvent.Create("Cooking", "native_bonus_roll")
            .String("source", source)
            .String("path", path)
            .String("skill", skill.ToString())
            .Boolean("cooking_gate", cookingGate)
            .Number("configured_base_chance", configuredBaseChance)
            .Number("native_skill_factor", nativeSkillFactor)
            .Number("effective_chance", effectiveChance)
            .Number("roll", nativeRoll)
            .Boolean("succeeded", succeeded)
            .Integer("configured_bonus_amount", configuredBonusAmount)
            .Integer("bonus_count", bonusCount);
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
            AccessTools.Method(typeof(CookingBonus), nameof(CookingBonus.RollForCrafting)),
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
            AccessTools.Method(typeof(CookingBonus), nameof(CookingBonus.RollForCookingStation)),
            "CookingStation.OnInteract");
    }
}

internal static class CookingBonusTranspiler
{
    internal static IEnumerable<CodeInstruction> ReplaceNativeChance(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo? replacement,
        MethodInfo? rollObserver,
        string seam)
    {
        if (replacement == null)
        {
            throw new InvalidOperationException($"Cooking bonus replacement was not found for {seam}.");
        }
        if (rollObserver == null)
        {
            throw new InvalidOperationException($"Cooking bonus roll observer was not found for {seam}.");
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
        for (int index = 0; index < codes.Count; index++)
        {
            CodeInstruction code = codes[index];
            if (code.opcode != OpCodes.Ldfld || !Equals(code.operand, nativeChance))
            {
                continue;
            }

            if (index < 3
                || index + 3 >= codes.Count
                || !IsNativeRandomValueCall(codes[index - 3])
                || codes[index + 1].opcode != OpCodes.Mul
                || !IsGreaterThanOrEqualBranch(codes[index + 2].opcode)
                || !IsLocalLoad(codes[index + 3].opcode))
            {
                throw new InvalidOperationException(
                    $"Valheim's native craft bonus roll changed in {seam}.");
            }

            // The native InventoryGui instance already on the stack becomes
            // the resolver argument. The already-consumed roll, skill factor,
            // configured chance, and native bonus amount remain unchanged.
            code.opcode = OpCodes.Call;
            code.operand = replacement;
            codes[index + 1].opcode = codes[index + 3].opcode;
            codes[index + 1].operand = codes[index + 3].operand;
            codes.Insert(index + 2, new CodeInstruction(OpCodes.Call, rollObserver));
            codes[index + 3].opcode = codes[index + 3].opcode == OpCodes.Bge_Un_S
                ? OpCodes.Brfalse_S
                : OpCodes.Brfalse;
            replaced++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException(
                $"Expected one native craft bonus chance read in {seam}, found {replaced}.");
        }

        return codes;
    }

    private static bool IsNativeRandomValueCall(CodeInstruction code)
    {
        MethodInfo? nativeRandomValue = typeof(UnityEngine.Random)
            .GetProperty(nameof(UnityEngine.Random.value))?
            .GetGetMethod();
        return code.opcode == OpCodes.Call && Equals(code.operand, nativeRandomValue);
    }

    private static bool IsGreaterThanOrEqualBranch(OpCode opcode)
    {
        return opcode == OpCodes.Bge_Un || opcode == OpCodes.Bge_Un_S;
    }

    private static bool IsLocalLoad(OpCode opcode)
    {
        return opcode == OpCodes.Ldloc
            || opcode == OpCodes.Ldloc_S
            || opcode == OpCodes.Ldloc_0
            || opcode == OpCodes.Ldloc_1
            || opcode == OpCodes.Ldloc_2
            || opcode == OpCodes.Ldloc_3;
    }
}
