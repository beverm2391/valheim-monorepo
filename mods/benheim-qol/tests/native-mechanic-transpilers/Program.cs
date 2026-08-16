using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BenheimQoL.Interaction;
using BenheimQoL.Production;
using HarmonyLib;

FieldInfo chanceField = typeof(InventoryGui).GetField(nameof(InventoryGui.m_craftBonusChance))!;
MethodInfo craftResolver = Resolver(nameof(CookingBonus.ForCrafting));
MethodInfo stationResolver = Resolver(nameof(CookingBonus.ForCookingStation));

VerifyCookingPatch(typeof(CookingCraftBonusPatch), chanceField, craftResolver);
VerifyCookingPatch(typeof(CookingStationBonusPatch), chanceField, stationResolver);
VerifyCookingScope();
VerifyComfortPatch();

System.Console.WriteLine("native mechanic transpiler behavior checks passed");
return;

static MethodInfo Resolver(string name)
{
    return typeof(CookingBonus).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Missing Cooking bonus resolver {name}.");
}

static void VerifyCookingPatch(Type patchType, FieldInfo chanceField, MethodInfo resolver)
{
    CodeInstruction nativeRead = new CodeInstruction(OpCodes.Ldfld, chanceField);
    VerifyReplacement(patchType, Frame(nativeRead), nativeRead, OpCodes.Call, resolver);
    ExpectThrows(() => Invoke(patchType, Frame(new CodeInstruction(OpCodes.Nop))));
    ExpectThrows(() => Invoke(
        patchType,
        Frame(
            new CodeInstruction(OpCodes.Ldfld, chanceField),
            new CodeInstruction(OpCodes.Ldfld, chanceField))));
}

static void VerifyCookingScope()
{
    InventoryGui inventoryGui = new InventoryGui { m_craftBonusChance = 0.25f };
    Player.m_localPlayer = new Player(new CraftingStation
    {
        m_craftingSkill = Skills.SkillType.Cooking
    });
    Expect(CookingBonus.ForCrafting(inventoryGui) == 0.50f);

    Player.m_localPlayer = new Player(new CraftingStation
    {
        m_craftingSkill = Skills.SkillType.Other
    });
    Expect(CookingBonus.ForCrafting(inventoryGui) == 0.25f);
    Player.m_localPlayer = null;
    Expect(CookingBonus.ForCrafting(inventoryGui) == 0.25f);
    Expect(CookingBonus.ForCookingStation(inventoryGui) == 0.50f);
}

static void VerifyComfortPatch()
{
    CodeInstruction nativeRadius = new CodeInstruction(OpCodes.Ldc_R4, 10f);
    VerifyReplacement(
        typeof(ComfortFurnitureRangePatch),
        Frame(nativeRadius),
        nativeRadius,
        OpCodes.Ldc_R4,
        20f);
    ExpectThrows(() => Invoke(
        typeof(ComfortFurnitureRangePatch),
        Frame(new CodeInstruction(OpCodes.Ldc_R4, 9f))));
    ExpectThrows(() => Invoke(
        typeof(ComfortFurnitureRangePatch),
        Frame(
            new CodeInstruction(OpCodes.Ldc_R4, 10f),
            new CodeInstruction(OpCodes.Ldc_R4, 10f))));
}

static void VerifyReplacement(
    Type patchType,
    List<CodeInstruction> input,
    CodeInstruction target,
    OpCode expectedOpCode,
    object expectedOperand)
{
    CodeInstruction[] originalObjects = input.ToArray();
    List<CodeInstruction> output = Invoke(patchType, input);
    Expect(output.SequenceEqual(originalObjects));
    Expect(output[0].opcode == OpCodes.Nop && output[0].operand == null);
    Expect(output[^1].opcode == OpCodes.Ret && output[^1].operand == null);
    Expect(target.opcode == expectedOpCode && Equals(target.operand, expectedOperand));
    Expect(output.All(instruction => instruction.labels.Count == 1 && instruction.blocks.Count == 1));
}

static List<CodeInstruction> Frame(params CodeInstruction[] middle)
{
    List<CodeInstruction> instructions = new List<CodeInstruction>
    {
        new CodeInstruction(OpCodes.Nop)
    };
    instructions.AddRange(middle);
    instructions.Add(new CodeInstruction(OpCodes.Ret));
    for (int index = 0; index < instructions.Count; index++)
    {
        instructions[index].labels.Add(default);
        instructions[index].blocks.Add(new ExceptionBlock(index));
    }
    return instructions;
}

static List<CodeInstruction> Invoke(Type patchType, IEnumerable<CodeInstruction> instructions)
{
    MethodInfo transpiler = patchType.GetMethod(
        "Transpiler",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Missing {patchType.Name}.Transpiler.");
    try
    {
        return ((IEnumerable<CodeInstruction>)transpiler.Invoke(
            null,
            new object[] { instructions })!).ToList();
    }
    catch (TargetInvocationException exception) when (exception.InnerException != null)
    {
        throw exception.InnerException;
    }
}

static void ExpectThrows(Action action)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }
    throw new InvalidOperationException("Expected the transpiler to reject a changed native seam.");
}

static void Expect(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Native mechanic transpiler assertion failed.");
    }
}
