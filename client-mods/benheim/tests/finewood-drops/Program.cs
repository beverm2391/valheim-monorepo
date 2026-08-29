using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BenheimQoL.Woodcutting;
using HarmonyLib;
using UnityEngine;

GameObject wood = new GameObject("Wood");
GameObject finewood = new GameObject("FineWood");
GameObject resin = new GameObject("Resin");
ObjectDB objectDb = new ObjectDB();
objectDb.Add(finewood);
ObjectDB.instance = objectDb;

foreach (string prefabName in new[]
         {
             "Birch_log",
             "Birch_log_half",
             "Oak_log",
             "Oak_log_half"
         })
{
    TreeLog log = new TreeLog(prefabName);
    Expect(ReferenceEquals(FinewoodDrops.ConvertNativeWood(wood, log), finewood));
    Expect(ReferenceEquals(FinewoodDrops.ConvertNativeWood(finewood, log), finewood));
    Expect(ReferenceEquals(FinewoodDrops.ConvertNativeWood(resin, log), resin));
}

foreach (string prefabName in new[]
         {
             "beech_log",
             "FirTree_log",
             "PineTree_log",
             "BirchStub",
             "Oak1"
         })
{
    TreeLog log = new TreeLog(prefabName);
    Expect(ReferenceEquals(FinewoodDrops.ConvertNativeWood(wood, log), wood));
    Expect(ReferenceEquals(FinewoodDrops.ConvertNativeWood(resin, log), resin));
}

ObjectDB.instance = new ObjectDB();
Expect(ReferenceEquals(
    FinewoodDrops.ConvertNativeWood(wood, new TreeLog("Birch_log_half")),
    wood));

VerifyOwnerDropSeamTranspiler();

Console.WriteLine("finewood drop behavior checks passed");
return;

static void VerifyOwnerDropSeamTranspiler()
{
    MethodInfo nativeConversion = typeof(Game).GetMethod(
        nameof(Game.CheckDropConversion),
        new[]
        {
            typeof(HitData),
            typeof(ItemDrop),
            typeof(GameObject),
            typeof(int).MakeByRefType()
        })!;
    MethodInfo conversion = typeof(FinewoodDrops).GetMethod(
        nameof(FinewoodDrops.ConvertNativeWood),
        BindingFlags.NonPublic | BindingFlags.Static,
        binder: null,
        types: new[] { typeof(GameObject), typeof(TreeLog) },
        modifiers: null)!;
    List<CodeInstruction> input = Frame(
        new CodeInstruction(OpCodes.Ldarg_0),
        new CodeInstruction(OpCodes.Callvirt, nativeConversion),
        new CodeInstruction(OpCodes.Stloc_0));
    CodeInstruction[] originals = input.ToArray();
    List<CodeInstruction> output = InvokeTranspiler(input);

    Expect(output.Count == input.Count + 2);
    Expect(originals.All(output.Contains));
    int nativeCall = output.FindIndex(instruction => instruction.Calls(nativeConversion));
    Expect(nativeCall >= 0);
    Expect(output[nativeCall + 1].opcode == OpCodes.Ldarg_0);
    Expect(output[nativeCall + 2].opcode == OpCodes.Call);
    Expect(Equals(output[nativeCall + 2].operand, conversion));

    ExpectThrows(() => InvokeTranspiler(Frame(new CodeInstruction(OpCodes.Nop))));
    ExpectThrows(() => InvokeTranspiler(Frame(
        new CodeInstruction(OpCodes.Callvirt, nativeConversion),
        new CodeInstruction(OpCodes.Callvirt, nativeConversion))));
}

static List<CodeInstruction> Frame(params CodeInstruction[] middle)
{
    List<CodeInstruction> instructions = new List<CodeInstruction>
    {
        new CodeInstruction(OpCodes.Nop)
    };
    instructions.AddRange(middle);
    instructions.Add(new CodeInstruction(OpCodes.Ret));
    return instructions;
}

static List<CodeInstruction> InvokeTranspiler(IEnumerable<CodeInstruction> instructions)
{
    MethodInfo transpiler = typeof(FinewoodDropPatches).GetMethod(
        "Transpiler",
        BindingFlags.NonPublic | BindingFlags.Static)!;
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

    throw new InvalidOperationException("Expected changed native Finewood seam to fail closed.");
}

static void Expect(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Finewood drop assertion failed.");
    }
}
