using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BenheimQoL.Woodcutting;

[HarmonyPatch(typeof(TreeLog), "Destroy", new[] { typeof(HitData) })]
internal static class FinewoodDropPatches
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo nativeDropConversion = AccessTools.Method(
                typeof(Game),
                nameof(Game.CheckDropConversion),
                new[]
                {
                    typeof(HitData),
                    typeof(ItemDrop),
                    typeof(UnityEngine.GameObject),
                    typeof(int).MakeByRefType()
                })
            ?? throw new MissingMethodException(typeof(Game).FullName, nameof(Game.CheckDropConversion));
        MethodInfo conversion = AccessTools.Method(
                typeof(FinewoodDrops),
                nameof(FinewoodDrops.ConvertNativeWood),
                new[] { typeof(UnityEngine.GameObject), typeof(TreeLog) })
            ?? throw new MissingMethodException(
                typeof(FinewoodDrops).FullName,
                nameof(FinewoodDrops.ConvertNativeWood));

        List<CodeInstruction> output = new List<CodeInstruction>();
        int patched = 0;
        foreach (CodeInstruction instruction in instructions)
        {
            output.Add(instruction);
            if (!instruction.Calls(nativeDropConversion))
            {
                continue;
            }

            // TreeLog.Destroy runs only inside the owner's RPC_Damage path.
            // Valheim has already realized the list and applied damage-type
            // conversions. Replace only a final Wood prefab before its native
            // spawn loop sees it; its count, position, and effects stay native.
            output.Add(new CodeInstruction(OpCodes.Ldarg_0));
            output.Add(new CodeInstruction(OpCodes.Call, conversion));
            patched++;
        }

        if (patched != 1)
        {
            throw new InvalidOperationException(
                $"Expected one TreeLog destruction drop-conversion seam, found {patched}.");
        }

        return output;
    }
}
