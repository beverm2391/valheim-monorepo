using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BenheimQoL.Interaction;

/// <summary>
/// Changes only the radius passed to Valheim's native comfort-piece query.
/// The native calculation still owns shelter checks, piece filtering, comfort
/// groups, duplicate handling, comfort values, and Rested duration.
/// </summary>
[HarmonyPatch(typeof(SE_Rested), "GetNearbyComfortPieces")]
internal static class ComfortFurnitureRangePatch
{
    internal const float NativeComfortRadius = 10f;
    internal const float ExtendedComfortRadius = 20f;
    private static readonly MethodInfo ObserveRadiusMethod = AccessTools.Method(
        typeof(ComfortDiagnosticCapture),
        nameof(ComfortDiagnosticCapture.ObserveRadius))
        ?? throw new MissingMethodException(
            typeof(ComfortDiagnosticCapture).FullName,
            nameof(ComfortDiagnosticCapture.ObserveRadius));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        int replaced = 0;

        for (int index = 0; index < codes.Count; index++)
        {
            CodeInstruction code = codes[index];
            if (code.opcode != OpCodes.Ldc_R4
                || !(code.operand is float radius)
                || radius != NativeComfortRadius)
            {
                continue;
            }

            code.operand = ExtendedComfortRadius;
            codes.Insert(index + 1, new CodeInstruction(OpCodes.Call, ObserveRadiusMethod));
            replaced++;
            index++;
        }

        if (replaced != 1)
        {
            throw new InvalidOperationException(
                $"Expected one native comfort radius of {NativeComfortRadius}, found {replaced}.");
        }

        return codes;
    }
}
