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

        // The native hit pipeline gained several locals in 1.0.  The direct
        // IDestructible call is still the semantic boundary we need: its two
        // immediately preceding loads are the target and the fully populated
        // HitData.  Resolve those locals from the actual IL rather than
        // pinning compiler-assigned local slots.
        int targetLoadIndex = damageCallIndex - 2;
        if (targetLoadIndex < 0
            || !TryGetLoadLocalIndex(codes[targetLoadIndex], out int targetLocal)
            || !TryGetLoadLocalIndex(codes[targetLoadIndex + 1], out int hitLocal))
        {
            throw new InvalidOperationException(
                "Projectile damage seam no longer loads target and HitData locals; refusing to install headshots.");
        }

        CodeInstruction loadProjectile = new CodeInstruction(OpCodes.Ldarg_0);
        codes[targetLoadIndex].MoveLabelsTo(loadProjectile);
        codes[targetLoadIndex].MoveBlocksTo(loadProjectile);
        codes.InsertRange(
            targetLoadIndex,
            new[]
            {
                loadProjectile,
                CreateLoadLocal(targetLocal),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldarg_2),
                CreateLoadLocal(hitLocal),
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

    private static CodeInstruction CreateLoadLocal(int index)
    {
        return new CodeInstruction(OpCodes.Ldloc, index);
    }

    private static bool TryGetLoadLocalIndex(CodeInstruction instruction, out int index)
    {
        if (instruction.opcode == OpCodes.Ldloc_0)
        {
            index = 0;
            return true;
        }
        if (instruction.opcode == OpCodes.Ldloc_1)
        {
            index = 1;
            return true;
        }
        if (instruction.opcode == OpCodes.Ldloc_2)
        {
            index = 2;
            return true;
        }
        if (instruction.opcode == OpCodes.Ldloc_3)
        {
            index = 3;
            return true;
        }

        if (instruction.opcode != OpCodes.Ldloc_S && instruction.opcode != OpCodes.Ldloc)
        {
            index = -1;
            return false;
        }

        switch (instruction.operand)
        {
            case byte value:
                index = value;
                return true;
            case sbyte value:
                index = value;
                return true;
            case short value:
                index = value;
                return true;
            case ushort value:
                index = value;
                return true;
            case int value:
                index = value;
                return true;
            case LocalBuilder local:
                index = local.LocalIndex;
                return true;
            default:
                index = -1;
                return false;
        }
    }
}
