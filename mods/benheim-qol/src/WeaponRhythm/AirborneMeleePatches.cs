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
            new[] { typeof(IDestructible), typeof(HitData) });
        if (replacement == null)
        {
            throw new InvalidOperationException("Airborne melee damage seam was not found.");
        }

        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replaced = 0;
        foreach (CodeInstruction code in codes)
        {
            if (!IsDirectDamageCall(code))
            {
                continue;
            }

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
