using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BenheimQoL.WeaponRhythm;

/// <summary>
/// Redirects only Attack's direct melee-to-target damage calls. This keeps the
/// native animation, hit geometry, HitData construction, and target-owner RPC
/// intact while excluding projectile and unrelated Character.Damage paths.
/// </summary>
[HarmonyPatch]
internal static class AirborneMeleePatches
{
    private const string AreaHitMethod = "<DoAreaAttack>g__checkHits|26_0";

    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return RequireAttackMethod("DoMeleeAttack");
        yield return RequireAttackMethod(AreaHitMethod);
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase __originalMethod)
    {
        MethodInfo? replacement = AccessTools.Method(
            typeof(AirborneMelee),
            nameof(AirborneMelee.DamageMeleeTarget),
            new[] { typeof(IDestructible), typeof(HitData), typeof(Attack) });
        if (replacement == null)
        {
            throw new InvalidOperationException("Airborne melee damage seam was not found.");
        }

        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replaced = 0;
        for (int index = 0; index < codes.Count; index++)
        {
            CodeInstruction code = codes[index];
            if (!IsDirectDamageCall(code))
            {
                continue;
            }

            CodeInstruction loadAttack = new CodeInstruction(OpCodes.Ldarg_0);
            code.MoveLabelsTo(loadAttack);
            code.MoveBlocksTo(loadAttack);
            codes.Insert(index, loadAttack);
            index++;
            code.opcode = OpCodes.Call;
            code.operand = replacement;
            replaced++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException(
                $"Expected one melee damage call in {__originalMethod.Name}, found {replaced}.");
        }
        return codes;
    }

    private static MethodInfo RequireAttackMethod(string name)
    {
        return AccessTools.Method(typeof(Attack), name)
            ?? throw new InvalidOperationException($"Required Attack method was not found: {name}");
    }

    private static bool IsDirectDamageCall(CodeInstruction instruction)
    {
        if (instruction.opcode != OpCodes.Callvirt || instruction.operand is not MethodInfo method)
        {
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        return method.DeclaringType == typeof(IDestructible)
            && method.Name == nameof(IDestructible.Damage)
            && parameters.Length == 1
            && parameters[0].ParameterType == typeof(HitData);
    }
}

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
internal static class AirborneMeleeStartPatch
{
    private static readonly FieldInfo PrimaryPressed = RequireInputField("m_attack");
    private static readonly FieldInfo SecondaryPressed = RequireInputField("m_secondaryAttack");

    [HarmonyPrefix]
    private static void Prefix(
        Humanoid __instance,
        bool secondaryAttack,
        out AirborneMeleeStartAttempt? __state)
    {
        FieldInfo inputField = secondaryAttack ? SecondaryPressed : PrimaryPressed;
        bool freshInput = (bool)(inputField.GetValue(__instance) ?? false);
        __state = AirborneMelee.BeginAttackAttempt(
            __instance,
            secondaryAttack,
            freshInput);
    }

    [HarmonyPostfix]
    private static void Postfix(
        bool __result,
        Attack? ___m_currentAttack,
        AirborneMeleeStartAttempt? __state)
    {
        AirborneMelee.CompleteAttackAttempt(__state, ___m_currentAttack, __result);
    }

    private static FieldInfo RequireInputField(string name)
    {
        return AccessTools.Field(typeof(Character), name)
            ?? throw new MissingFieldException(typeof(Character).FullName, name);
    }
}

[HarmonyPatch(typeof(Attack), nameof(Attack.Update))]
internal static class AirborneMeleeProgressPatch
{
    [HarmonyPostfix]
    private static void Postfix(Attack __instance)
    {
        AirborneMelee.ObserveAttackProgress(__instance);
    }
}

[HarmonyPatch(typeof(Attack), nameof(Attack.Stop))]
internal static class AirborneMeleeStopPatch
{
    [HarmonyPostfix]
    private static void Postfix(Attack __instance)
    {
        AirborneMelee.ObserveAttackStop(__instance);
    }
}
