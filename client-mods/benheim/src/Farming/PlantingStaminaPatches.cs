using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BenheimQoL.Farming;

[HarmonyPatch]
internal static class PlantingStaminaPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "GetBuildStamina")]
    private static void GetBuildStaminaPostfix(PieceTable ___m_buildPieces, ref float __result)
    {
        PlantingStamina.ApplyResolvedCost(___m_buildPieces, ref __result);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var output = new List<CodeInstruction>(instructions);
        MethodInfo getSelectedPiece = AccessTools.Method(typeof(PieceTable), nameof(PieceTable.GetSelectedPiece))
            ?? throw new MissingMethodException(typeof(PieceTable).FullName, nameof(PieceTable.GetSelectedPiece));
        MethodInfo haveStamina = AccessTools.Method(typeof(Character), nameof(Character.HaveStamina))
            ?? throw new MissingMethodException(typeof(Character).FullName, nameof(Character.HaveStamina));
        MethodInfo tryPlacePiece = AccessTools.Method(typeof(Player), nameof(Player.TryPlacePiece))
            ?? throw new MissingMethodException(typeof(Player).FullName, nameof(Player.TryPlacePiece));
        MethodInfo replacement = AccessTools.Method(typeof(PlantingStamina), nameof(PlantingStamina.HasPlacementStamina))
            ?? throw new MissingMethodException(typeof(PlantingStamina).FullName, nameof(PlantingStamina.HasPlacementStamina));

        int selectedPieceCall = FindUniqueCall(output, getSelectedPiece, 0, output.Count);
        int selectedPieceStore = selectedPieceCall + 1;
        if (selectedPieceStore >= output.Count || !TryCreateLocalLoad(output[selectedPieceStore], out CodeInstruction selectedPieceLoad))
        {
            throw new InvalidOperationException("Expected selected Piece local immediately after PieceTable.GetSelectedPiece.");
        }

        int tryPlaceCall = FindUniqueCall(output, tryPlacePiece, selectedPieceStore + 1, output.Count);
        int staminaCall = FindUniqueCall(output, haveStamina, selectedPieceStore + 1, tryPlaceCall);
        CodeInstruction nativeCall = output[staminaCall];
        nativeCall.MoveLabelsTo(selectedPieceLoad);
        nativeCall.MoveBlocksTo(selectedPieceLoad);
        output.Insert(staminaCall, selectedPieceLoad);
        nativeCall.opcode = OpCodes.Call;
        nativeCall.operand = replacement;
        return output;
    }

    private static int FindUniqueCall(
        IReadOnlyList<CodeInstruction> instructions,
        MethodInfo method,
        int start,
        int end)
    {
        int found = -1;
        for (int index = start; index < end; index++)
        {
            CodeInstruction instruction = instructions[index];
            if ((instruction.opcode != OpCodes.Call && instruction.opcode != OpCodes.Callvirt)
                || !Equals(instruction.operand, method))
            {
                continue;
            }

            if (found >= 0)
            {
                throw new InvalidOperationException($"Expected one {method.Name} call in the planting placement seam.");
            }

            found = index;
        }

        return found >= 0
            ? found
            : throw new InvalidOperationException($"Expected one {method.Name} call in the planting placement seam.");
    }

    private static bool TryCreateLocalLoad(CodeInstruction store, out CodeInstruction load)
    {
        OpCode loadCode;
        if (store.opcode == OpCodes.Stloc_0)
        {
            loadCode = OpCodes.Ldloc_0;
        }
        else if (store.opcode == OpCodes.Stloc_1)
        {
            loadCode = OpCodes.Ldloc_1;
        }
        else if (store.opcode == OpCodes.Stloc_2)
        {
            loadCode = OpCodes.Ldloc_2;
        }
        else if (store.opcode == OpCodes.Stloc_3)
        {
            loadCode = OpCodes.Ldloc_3;
        }
        else if (store.opcode == OpCodes.Stloc_S)
        {
            loadCode = OpCodes.Ldloc_S;
        }
        else if (store.opcode == OpCodes.Stloc)
        {
            loadCode = OpCodes.Ldloc;
        }
        else
        {
            load = null!;
            return false;
        }

        load = new CodeInstruction(loadCode, store.operand);
        return true;
    }
}
