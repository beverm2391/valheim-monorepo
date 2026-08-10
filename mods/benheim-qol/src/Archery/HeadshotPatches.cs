using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BenheimQoL.Archery;

/// <summary>
/// Injects immediately before Projectile's direct collision Damage call. This
/// keeps the real collider, impact point, start point, and freshly-built
/// HitData together without a cross-frame or per-projectile state store.
/// </summary>
[HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]
internal static class ProjectileHeadshotPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        MethodInfo? applyMethod = AccessTools.Method(
            typeof(HeadshotLogic),
            nameof(HeadshotLogic.Apply),
            new[]
            {
                typeof(Projectile),
                typeof(IDestructible),
                typeof(UnityEngine.Collider),
                typeof(UnityEngine.Vector3),
                typeof(HitData)
            });
        if (applyMethod == null)
        {
            throw new InvalidOperationException("Headshot apply seam method was not found.");
        }

        int damageCallIndex = -1;
        int damageCallCount = 0;
        for (int i = 0; i < codes.Count; i++)
        {
            if (!IsDirectDamageCall(codes[i]))
            {
                continue;
            }

            damageCallIndex = i;
            damageCallCount++;
        }

        if (damageCallCount != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one direct Projectile damage call, found {damageCallCount}.");
        }

        int targetLoadIndex = damageCallIndex - 2;
        if (targetLoadIndex < 0
            || !IsLoadLocal(codes[targetLoadIndex], 4)
            || !IsLoadLocal(codes[targetLoadIndex + 1], 10))
        {
            throw new InvalidOperationException(
                "Projectile damage seam locals changed; refusing to install headshots.");
        }

        codes.InsertRange(
            targetLoadIndex,
            new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_S, (byte)4),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldloc_S, (byte)10),
                new CodeInstruction(OpCodes.Call, applyMethod)
            });
        return codes;
    }

    private static bool IsDirectDamageCall(CodeInstruction instruction)
    {
        if (instruction.opcode != OpCodes.Callvirt
            || !(instruction.operand is MethodInfo method))
        {
            return false;
        }

        ParameterInfo[] parameters = method.GetParameters();
        return method.DeclaringType == typeof(IDestructible)
            && method.Name == nameof(IDestructible.Damage)
            && parameters.Length == 1
            && parameters[0].ParameterType == typeof(HitData);
    }

    private static bool IsLoadLocal(CodeInstruction instruction, int index)
    {
        if (index == 0 && instruction.opcode == OpCodes.Ldloc_0
            || index == 1 && instruction.opcode == OpCodes.Ldloc_1
            || index == 2 && instruction.opcode == OpCodes.Ldloc_2
            || index == 3 && instruction.opcode == OpCodes.Ldloc_3)
        {
            return true;
        }

        if (instruction.opcode != OpCodes.Ldloc_S && instruction.opcode != OpCodes.Ldloc)
        {
            return false;
        }

        return instruction.operand switch
        {
            byte value => value == index,
            sbyte value => value == index,
            short value => value == index,
            ushort value => value == index,
            int value => value == index,
            LocalBuilder local => local.LocalIndex == index,
            _ => false
        };
    }
}
