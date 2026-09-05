using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BenheimQoL.Interaction;
using BenheimQoL.Production;
using BenheimQoL.Farming;
using HarmonyLib;

FieldInfo chanceField = typeof(InventoryGui).GetField(nameof(InventoryGui.m_craftBonusChance))!;
MethodInfo craftResolver = Resolver(nameof(CookingBonus.ForCrafting));
MethodInfo stationResolver = Resolver(nameof(CookingBonus.ForCookingStation));
MethodInfo craftRollObserver = Resolver(nameof(CookingBonus.RollForCrafting));
MethodInfo stationRollObserver = Resolver(nameof(CookingBonus.RollForCookingStation));
MethodInfo randomValue = typeof(UnityEngine.Random).GetProperty(nameof(UnityEngine.Random.value))!.GetGetMethod()!;

VerifyCookingPatch(
    typeof(CookingCraftBonusPatch),
    chanceField,
    craftResolver,
    craftRollObserver,
    randomValue);
VerifyCookingPatch(
    typeof(CookingStationBonusPatch),
    chanceField,
    stationResolver,
    stationRollObserver,
    randomValue);
VerifyCookingScope();
VerifyCookingRollObservation();
VerifyComfortPatch();
StationBuildCoverageTests.Run();
VerifyTarPolicy();
VerifyTarTranspilers();
VerifyPlantingStamina();

System.Console.WriteLine("native mechanic transpiler behavior checks passed");
return;

static MethodInfo Resolver(string name)
{
    return typeof(CookingBonus).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Missing Cooking bonus resolver {name}.");
}

static void VerifyPlantingStamina()
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
    // Player overrides Character.HaveStamina, but installed Valheim 0.221.12 calls the
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
    List<CodeInstruction> output = Invoke(typeof(PlantingStaminaPatches), input);
    int replacementIndex = output.FindIndex(instruction => Equals(instruction.operand, replacement));
    Expect(replacementIndex > 0);
    Expect(output[replacementIndex].opcode == OpCodes.Call);
    Expect(output[replacementIndex - 1].opcode == OpCodes.Ldloc_2);
    Expect(output[replacementIndex - 1].labels.Count == 1);
    Expect(output[replacementIndex - 1].blocks.Count == 1);
    Expect(output[replacementIndex].labels.Count == 0);
    Expect(output[replacementIndex].blocks.Count == 0);
    Expect(output.Count == input.Count + 1);

    ExpectThrows(() => Invoke(
        typeof(PlantingStaminaPatches),
        Frame(
            new CodeInstruction(OpCodes.Callvirt, getSelectedPiece),
            new CodeInstruction(OpCodes.Stloc_2),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Callvirt, tryPlacePiece))));
}

static void VerifyCookingPatch(
    Type patchType,
    FieldInfo chanceField,
    MethodInfo resolver,
    MethodInfo rollObserver,
    MethodInfo randomValue)
{
    CodeInstruction randomCall = new CodeInstruction(OpCodes.Call, randomValue);
    CodeInstruction nativeRead = new CodeInstruction(OpCodes.Ldfld, chanceField);
    CodeInstruction multiply = new CodeInstruction(OpCodes.Mul);
    CodeInstruction failureBranch = new CodeInstruction(OpCodes.Bge_Un_S, default(Label));
    CodeInstruction nativeCountLoad = new CodeInstruction(OpCodes.Ldloc_1);
    List<CodeInstruction> input = Frame(
        randomCall,
        new CodeInstruction(OpCodes.Ldloc_0),
        new CodeInstruction(OpCodes.Ldarg_0),
        nativeRead,
        multiply,
        failureBranch,
        nativeCountLoad,
        new CodeInstruction(OpCodes.Nop));
    CodeInstruction[] originalObjects = input.ToArray();
    List<CodeInstruction> output = Invoke(patchType, input);
    Expect(output.Count == originalObjects.Length + 1);
    Expect(originalObjects.All(output.Contains));
    Expect(nativeRead.opcode == OpCodes.Call && Equals(nativeRead.operand, resolver));
    Expect(multiply.opcode == nativeCountLoad.opcode && Equals(multiply.operand, nativeCountLoad.operand));
    int observerIndex = output.IndexOf(multiply) + 1;
    Expect(output[observerIndex].opcode == OpCodes.Call && Equals(output[observerIndex].operand, rollObserver));
    Expect(failureBranch.opcode == OpCodes.Brfalse_S);
    Expect(originalObjects.All(instruction => instruction.labels.Count == 1 && instruction.blocks.Count == 1));

    ExpectThrows(() => Invoke(patchType, Frame(new CodeInstruction(OpCodes.Nop))));
    ExpectThrows(() => Invoke(
        patchType,
        Frame(
            new CodeInstruction(OpCodes.Call, randomValue),
            new CodeInstruction(OpCodes.Ldloc_0),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, chanceField),
            new CodeInstruction(OpCodes.Mul),
            new CodeInstruction(OpCodes.Bge_Un_S, default(Label)),
            new CodeInstruction(OpCodes.Ldloc_1),
            new CodeInstruction(OpCodes.Call, randomValue),
            new CodeInstruction(OpCodes.Ldloc_0),
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldfld, chanceField),
            new CodeInstruction(OpCodes.Mul),
            new CodeInstruction(OpCodes.Bge_Un_S, default(Label)),
            new CodeInstruction(OpCodes.Ldloc_1))));
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

static void VerifyCookingRollObservation()
{
    InventoryGui inventoryGui = new InventoryGui
    {
        m_craftBonusChance = 0.25f,
        m_craftBonusAmount = 1
    };
    InventoryGui.m_instance = inventoryGui;
    Player.m_localPlayer = new Player(new CraftingStation
    {
        m_craftingSkill = Skills.SkillType.Cooking
    });
    int before = BenheimQoL.Infrastructure.Diagnostics.Emitted;
    Expect(CookingBonus.RollForCrafting(0.24f, 0.50f, 0.50f, 0));
    Expect(BenheimQoL.Infrastructure.Diagnostics.Last!.IntegerValue("bonus_count") == 1);
    Expect(BenheimQoL.Infrastructure.Diagnostics.Last.IntegerValue("native_bonus_count_after") == 1);
    Expect(BenheimQoL.Infrastructure.Diagnostics.Last.IntegerValue("native_result_increment") == 1);
    Expect(CookingBonus.RollForCrafting(0.24f, 0.50f, 0.50f, 1));
    Expect(BenheimQoL.Infrastructure.Diagnostics.Last!.IntegerValue("bonus_count") == 1);
    Expect(BenheimQoL.Infrastructure.Diagnostics.Last.IntegerValue("native_bonus_count_after") == 2);
    Expect(BenheimQoL.Infrastructure.Diagnostics.Last.IntegerValue("native_result_increment") == 2);
    Expect(!CookingBonus.RollForCrafting(0.25f, 0.50f, 0.50f, 2));
    Expect(BenheimQoL.Infrastructure.Diagnostics.Emitted == before + 3);

    Player.m_localPlayer = new Player(new CraftingStation
    {
        m_craftingSkill = Skills.SkillType.Other
    });
    before = BenheimQoL.Infrastructure.Diagnostics.Emitted;
    Expect(CookingBonus.RollForCrafting(0.24f, 1f, inventoryGui.m_craftBonusChance, 0));
    Expect(CookingBonus.RollForCrafting(0.24f, 1f, inventoryGui.m_craftBonusChance, 1));
    Expect(BenheimQoL.Infrastructure.Diagnostics.Emitted == before + 1);

    before = BenheimQoL.Infrastructure.Diagnostics.Emitted;
    Expect(CookingBonus.RollForCookingStation(0.24f, 0.50f, 0.50f, 1));
    Expect(BenheimQoL.Infrastructure.Diagnostics.Last!.IntegerValue("native_result_count") == 2);
    Expect(!CookingBonus.RollForCookingStation(0.25f, 0.50f, 0.50f, 1));
    Expect(BenheimQoL.Infrastructure.Diagnostics.Last!.IntegerValue("native_result_count") == 1);
    Expect(BenheimQoL.Infrastructure.Diagnostics.Emitted == before + 2);
}

static void VerifyComfortPatch()
{
    CodeInstruction nativeRadius = new CodeInstruction(OpCodes.Ldc_R4, 10f);
    List<CodeInstruction> output = Invoke(
        typeof(ComfortFurnitureRangePatch),
        Frame(nativeRadius));
    MethodInfo observer = typeof(ComfortDiagnosticCapture).GetMethod(
        nameof(ComfortDiagnosticCapture.ObserveRadius),
        BindingFlags.NonPublic | BindingFlags.Static)!;
    int observerIndex = output.FindIndex(instruction => Equals(instruction.operand, observer));
    Expect(output.Count == 4);
    Expect(nativeRadius.opcode == OpCodes.Ldc_R4 && Equals(nativeRadius.operand, 20f));
    Expect(observerIndex == 2 && output[observerIndex].opcode == OpCodes.Call);
    Expect(ComfortDiagnosticCapture.ObserveRadius(20f) == 20f);
    ExpectThrows(() => Invoke(
        typeof(ComfortFurnitureRangePatch),
        Frame(new CodeInstruction(OpCodes.Ldc_R4, 9f))));
    ExpectThrows(() => Invoke(
        typeof(ComfortFurnitureRangePatch),
        Frame(
            new CodeInstruction(OpCodes.Ldc_R4, 10f),
            new CodeInstruction(OpCodes.Ldc_R4, 10f))));
}

static void VerifyTarPolicy()
{
    Pickable smallTar = TarPickable("Pickable_Tar", tarGate: true);
    Expect(!TarCollectibleInteraction.ShouldBlockPickable(smallTar));

    Pickable ordinaryPickable = TarPickable("Pickable_Stone", tarGate: true);
    Expect(!TarCollectibleInteraction.ShouldBlockPickable(ordinaryPickable));

    foreach ((string prefab, string itemName) in new[]
             {
                 ("Tar", "$item_tar"),
                 ("Stone", "$item_stone"),
                 ("Wood", "$item_wood")
             })
    {
        ItemDrop itemDrop = TarItemDrop(
            prefab,
            itemName,
            ItemDrop.ItemData.ItemType.Material,
            inTar: true);
        Expect(!TarCollectibleInteraction.ShouldBlockItemDrop(itemDrop));

        itemDrop.TarState = false;
        Expect(!TarCollectibleInteraction.ShouldBlockItemDrop(itemDrop));
    }
}

static void VerifyTarTranspilers()
{
    FieldInfo pickableGate = typeof(Pickable).GetField(nameof(Pickable.m_tarPreventsPicking))!;
    MethodInfo pickableReplacement = typeof(TarCollectibleInteraction).GetMethod(
        nameof(TarCollectibleInteraction.ShouldBlockPickable),
        BindingFlags.NonPublic | BindingFlags.Static)!;
    CodeInstruction pickableRead = new CodeInstruction(OpCodes.Ldfld, pickableGate);
    VerifyReplacement(
        typeof(TarPickableInteractionPatch),
        Frame(pickableRead),
        pickableRead,
        OpCodes.Call,
        pickableReplacement);
    ExpectThrows(() => Invoke(
        typeof(TarPickableInteractionPatch),
        Frame(new CodeInstruction(OpCodes.Nop))));
    ExpectThrows(() => Invoke(
        typeof(TarPickableInteractionPatch),
        Frame(
            new CodeInstruction(OpCodes.Ldfld, pickableGate),
            new CodeInstruction(OpCodes.Ldfld, pickableGate))));

    MethodInfo itemDropGate = typeof(ItemDrop).GetMethod(nameof(ItemDrop.InTar))!;
    MethodInfo itemDropReplacement = typeof(TarCollectibleInteraction).GetMethod(
        nameof(TarCollectibleInteraction.ShouldBlockItemDrop),
        BindingFlags.NonPublic | BindingFlags.Static)!;
    VerifyItemDropTarGatePatch(
        typeof(TarItemDropInteractionPatch),
        itemDropGate,
        itemDropReplacement);
    VerifyItemDropTarGatePatch(
        typeof(TarItemDropAutoPickupPatch),
        itemDropGate,
        itemDropReplacement);
}

static void VerifyItemDropTarGatePatch(
    Type patchType,
    MethodInfo itemDropGate,
    MethodInfo itemDropReplacement)
{
    CodeInstruction itemDropCall = new CodeInstruction(OpCodes.Callvirt, itemDropGate);
    VerifyReplacement(
        patchType,
        Frame(itemDropCall),
        itemDropCall,
        OpCodes.Call,
        itemDropReplacement);
    ExpectThrows(() => Invoke(
        patchType,
        Frame(new CodeInstruction(OpCodes.Nop))));
    ExpectThrows(() => Invoke(
        patchType,
        Frame(
            new CodeInstruction(OpCodes.Call, itemDropGate),
            new CodeInstruction(OpCodes.Callvirt, itemDropGate))));
}

static Pickable TarPickable(string prefabName, bool tarGate)
{
    UnityEngine.GameObject itemPrefab = new UnityEngine.GameObject("Tar");
    itemPrefab.AddComponent(TarItemDrop(
        "Tar",
        "$item_tar",
        ItemDrop.ItemData.ItemType.Material,
        inTar: true));
    Pickable pickable = new Pickable
    {
        gameObject = new UnityEngine.GameObject(prefabName),
        m_itemPrefab = itemPrefab,
        m_tarPreventsPicking = tarGate
    };
    pickable.gameObject.AddComponent(new Floating { InTar = true });
    return pickable;
}

static ItemDrop TarItemDrop(
    string prefabName,
    string itemName,
    ItemDrop.ItemData.ItemType itemType,
    bool inTar)
{
    UnityEngine.GameObject prefab = new UnityEngine.GameObject(prefabName);
    ItemDrop itemDrop = new ItemDrop
    {
        gameObject = prefab,
        TarState = inTar,
        m_itemData = new ItemDrop.ItemData
        {
            m_dropPrefab = prefab,
            m_shared = new ItemDrop.ItemData.SharedData
            {
                m_name = itemName,
                m_itemType = itemType
            }
        }
    };
    prefab.AddComponent(itemDrop);
    return itemDrop;
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
