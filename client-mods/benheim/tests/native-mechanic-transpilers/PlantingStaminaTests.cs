using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BenheimQoL.Farming;
using HarmonyLib;

internal static class PlantingStaminaTests
{
    internal static void Run()
    {
        Expect(PlantingStamina.Cost(0f) == 0f);
        Expect(PlantingStamina.Cost(10f) == 2.5f);
        Expect(PlantingStamina.Cost(7.5f) == 1.875f);

        Piece plantPiece = new Piece();
        plantPiece.gameObject.AddComponent(new Plant());
        Piece berryPiece = new Piece();
        berryPiece.gameObject.name = "RaspberryBush";
        Piece ordinaryPiece = new Piece();
        PieceTable plantTable = new PieceTable(plantPiece);
        PieceTable berryTable = new PieceTable(berryPiece);
        PieceTable ordinaryTable = new PieceTable(ordinaryPiece);
        float resolvedCost = 10f;
        PlantingStamina.ApplyResolvedCost(plantTable, ref resolvedCost);
        Expect(resolvedCost == 2.5f);
        resolvedCost = 10f;
        PlantingStamina.ApplyResolvedCost(berryTable, ref resolvedCost);
        Expect(resolvedCost == 2.5f);
        resolvedCost = 10f;
        PlantingStamina.ApplyResolvedCost(ordinaryTable, ref resolvedCost);
        Expect(resolvedCost == 10f);

        Player player = new Player(station: null) { Stamina = 2.5f, ResolvedBuildStamina = 2.5f };
        Expect(PlantingStamina.HasPlacementStamina(player, 10f, plantPiece));
        Expect(player.LastStaminaCheck == 2.5f);
        Expect(PlantingStamina.HasPlacementStamina(player, 10f, berryPiece));
        Expect(player.LastStaminaCheck == 2.5f);
        Expect(!PlantingStamina.HasPlacementStamina(player, 10f, ordinaryPiece));
        Expect(player.LastStaminaCheck == 10f);

        MethodInfo getSelectedPiece = typeof(PieceTable).GetMethod(nameof(PieceTable.GetSelectedPiece))!;
        // Player overrides Character.HaveStamina, but Valheim 1.0.7 calls the
        // base-declared virtual slot from Player.UpdatePlacement.
        MethodInfo haveStamina = typeof(Character).GetMethod(nameof(Character.HaveStamina))!;
        MethodInfo tryPlacePiece = typeof(Player).GetMethod(nameof(Player.TryPlacePiece))!;
        MethodInfo replacement = typeof(PlantingStamina).GetMethod(
            nameof(PlantingStamina.HasPlacementStamina),
            BindingFlags.NonPublic | BindingFlags.Static)!;
        List<CodeInstruction> input = Frame(
            new CodeInstruction(OpCodes.Callvirt, getSelectedPiece),
            new CodeInstruction(OpCodes.Stloc_2),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldc_R4, 10f),
            new CodeInstruction(OpCodes.Callvirt, haveStamina),
            new CodeInstruction(OpCodes.Brfalse_S, default(Label)),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldloc_2),
            new CodeInstruction(OpCodes.Callvirt, tryPlacePiece));
        List<CodeInstruction> output = Invoke(input);
        int replacementIndex = output.FindIndex(instruction => Equals(instruction.operand, replacement));
        Expect(replacementIndex > 0);
        Expect(output[replacementIndex].opcode == OpCodes.Call);
        Expect(output[replacementIndex - 1].opcode == OpCodes.Ldloc_2);
        Expect(output[replacementIndex - 1].labels.Count == 1);
        Expect(output[replacementIndex - 1].blocks.Count == 1);
        Expect(output[replacementIndex].labels.Count == 0);
        Expect(output[replacementIndex].blocks.Count == 0);
        Expect(output.Count == input.Count + 1);

        ExpectThrows(() => Invoke(Frame(
            new CodeInstruction(OpCodes.Callvirt, getSelectedPiece),
            new CodeInstruction(OpCodes.Stloc_2),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Callvirt, tryPlacePiece))));
    }

    private static List<CodeInstruction> Frame(params CodeInstruction[] middle)
    {
        List<CodeInstruction> instructions = new List<CodeInstruction> { new CodeInstruction(OpCodes.Nop) };
        instructions.AddRange(middle);
        instructions.Add(new CodeInstruction(OpCodes.Ret));
        for (int index = 0; index < instructions.Count; index++)
        {
            instructions[index].labels.Add(default);
            instructions[index].blocks.Add(new ExceptionBlock(index));
        }
        return instructions;
    }

    private static List<CodeInstruction> Invoke(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo transpiler = typeof(PlantingStaminaPatches).GetMethod(
            "Transpiler",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Missing PlantingStaminaPatches.Transpiler.");
        try
        {
            return ((IEnumerable<CodeInstruction>)transpiler.Invoke(null, new object[] { instructions })!).ToList();
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            throw exception.InnerException;
        }
    }

    private static void ExpectThrows(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException("Expected the planting stamina transpiler to reject a changed native seam.");
    }

    private static void Expect(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Planting stamina assertion failed.");
        }
    }
}
