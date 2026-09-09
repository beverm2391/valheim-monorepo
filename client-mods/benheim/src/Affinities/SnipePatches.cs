using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Affinities;

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.GetAttackDrawPercentage))]
internal static class SnipeDrawPatch
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        MethodInfo nativeClamp = AccessTools.Method(typeof(Mathf), nameof(Mathf.Clamp01), new[] { typeof(float) });
        MethodInfo replacement = AccessTools.Method(typeof(SnipeRuntime), nameof(SnipeRuntime.ClampDrawPercentage));
        int matches = 0;
        for (int index = 0; index < codes.Count; index++)
        {
            CodeInstruction code = codes[index];
            if (code.opcode != OpCodes.Call || !Equals(code.operand, nativeClamp)) continue;
            // Installed Valheim computes elapsed / skill-adjusted duration
            // immediately before this clamp. Reject drift instead of silently
            // changing a different percentage or the already-clamped result.
            if (index == 0 || codes[index - 1].opcode != OpCodes.Div)
            {
                throw new InvalidOperationException("Snipe draw division/clamp seam changed.");
            }

            var character = new CodeInstruction(OpCodes.Ldarg_0);
            code.MoveLabelsTo(character);
            code.MoveBlocksTo(character);
            codes.Insert(index++, character);
            code.operand = replacement;
            matches++;
        }

        if (matches != 1)
        {
            throw new InvalidOperationException($"Expected one native draw clamp for Snipe, found {matches}.");
        }
        return codes;
    }
}

[HarmonyPatch(typeof(Projectile), nameof(Projectile.Setup))]
internal static class SnipeShotPatch
{
    private static void Postfix(Projectile __instance, Character owner, ItemDrop.ItemData item)
    {
        SnipeRuntime.ObserveShot(__instance, owner, item);
    }
}
