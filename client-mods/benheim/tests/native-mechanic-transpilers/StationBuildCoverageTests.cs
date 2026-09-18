using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using BenheimQoL.Interaction;
using HarmonyLib;

internal static class StationBuildCoverageTests
{
    internal static void Run()
    {
        CraftingStation workbench = Station(
            StationBuildCoverage.WorkbenchName,
            StationBuildCoverage.WorkbenchPrefab,
            x: 0f,
            y: 0f);
        CraftingStation stonecutter = Station(
            StationBuildCoverage.StonecutterName,
            StationBuildCoverage.StonecutterPrefab,
            x: 100f,
            y: 10f);
        CraftingStation forge = Station("$piece_forge", "forge", x: 200f, y: 0f);
        CraftingStation spoofedWorkbench = Station(
            StationBuildCoverage.WorkbenchName,
            "modded_workbench",
            x: 300f,
            y: 0f);
        CraftingStation.SetStations(workbench, stonecutter, forge, spoofedWorkbench);

        UnityEngine.Vector3 nativePoint = new UnityEngine.Vector3(10f, 100f, 0f);
        UnityEngine.Vector3 extendedPoint = new UnityEngine.Vector3(30f, 100f, 0f);
        UnityEngine.Vector3 beyondPoint = new UnityEngine.Vector3(40.1f, 100f, 0f);
        Expect(CraftingStation.HaveBuildStationInRange(StationBuildCoverage.WorkbenchName, nativePoint) == workbench);
        Expect(CraftingStation.HaveBuildStationInRange(StationBuildCoverage.WorkbenchName, extendedPoint) == null);
        Expect(StationBuildCoverage.FindForPlacement(StationBuildCoverage.WorkbenchName, nativePoint) == workbench);
        Expect(StationBuildCoverage.FindForPlacement("$piece_forge", Point(210f)) == forge);
        Expect(StationBuildCoverage.FindForPlacement(StationBuildCoverage.WorkbenchName, Point(310f)) == spoofedWorkbench);
        Expect(StationBuildCoverage.FindForPlacement(StationBuildCoverage.WorkbenchName, extendedPoint) == workbench);
        Expect(StationBuildCoverage.FindForPlacement(StationBuildCoverage.WorkbenchName, beyondPoint) == null);
        Expect(StationBuildCoverage.FindForPlacement(StationBuildCoverage.StonecutterName, Point(130f, 100f)) == stonecutter);
        Expect(StationBuildCoverage.FindForPlacement("$piece_forge", Point(230f)) == null);
        Expect(StationBuildCoverage.FindForPlacement(StationBuildCoverage.WorkbenchName, Point(330f)) == null);

        Expect(StationBuildCoverage.FindForMaintenance(StationBuildCoverage.WorkbenchName, extendedPoint) == workbench);
        Expect(StationBuildCoverage.FindForMaintenance(StationBuildCoverage.StonecutterName, Point(130f, 100f)) == stonecutter);
        Expect(StationBuildCoverage.FindForMaintenance(StationBuildCoverage.WorkbenchName, beyondPoint) == null);
        int maintenanceEvents = BenheimQoL.Infrastructure.Diagnostics.Emitted;
        Expect(StationBuildCoverage.FindForMaintenance(StationBuildCoverage.WorkbenchName, beyondPoint) == null);
        Expect(BenheimQoL.Infrastructure.Diagnostics.Emitted == maintenanceEvents);
        Expect(StationBuildCoverage.FindForMaintenance(StationBuildCoverage.WorkbenchName, extendedPoint) == workbench);
        Expect(BenheimQoL.Infrastructure.Diagnostics.Emitted == maintenanceEvents + 1);
        Expect(StationBuildCoverage.FindForMaintenance(StationBuildCoverage.WorkbenchName, extendedPoint) == workbench);
        Expect(BenheimQoL.Infrastructure.Diagnostics.Emitted == maintenanceEvents + 1);

        CircleProjector workbenchMarker = workbench.m_areaMarker.GetComponent<CircleProjector>()!;
        CircleProjector forgeMarker = forge.m_areaMarker.GetComponent<CircleProjector>()!;
        CircleProjector spoofedMarker = spoofedWorkbench.m_areaMarker.GetComponent<CircleProjector>()!;
        Expect(workbenchMarker.m_radius == 20f);
        Expect(StationBuildCoverage.FindForHammerUi(StationBuildCoverage.WorkbenchName, extendedPoint) == workbench);
        Expect(workbenchMarker.m_radius == 40f);
        Expect(StationBuildCoverage.FindForHammerUi("$piece_forge", Point(210f)) == forge);
        Expect(forgeMarker.m_radius == 20f);
        Expect(StationBuildCoverage.FindForHammerUi(StationBuildCoverage.WorkbenchName, Point(310f)) == spoofedWorkbench);
        Expect(spoofedMarker.m_radius == 20f);
        Expect(StationBuildCoverage.FindForHammerUi(StationBuildCoverage.WorkbenchName, beyondPoint) == null);

        workbench.NativeBuildRange = 28f;
        Expect(StationBuildCoverage.FindForPlacement(StationBuildCoverage.WorkbenchName, Point(55f)) == workbench);
        Expect(StationBuildCoverage.FindForHammerUi(StationBuildCoverage.WorkbenchName, Point(55f)) == workbench);
        Expect(workbenchMarker.m_radius == 56f);

        VerifyTranspilerShape(
            typeof(StationBuildPlacementCoveragePatch),
            nameof(StationBuildCoverage.FindForPlacement));
        VerifyTranspilerShape(
            typeof(StationBuildMaintenanceCoveragePatch),
            nameof(StationBuildCoverage.FindForMaintenance));
        VerifyTranspilerShape(
            typeof(StationBuildUiCoveragePatch),
            nameof(StationBuildCoverage.FindForHammerUi));
    }

    private static CraftingStation Station(string name, string prefab, float x, float y)
    {
        CraftingStation station = new CraftingStation
        {
            m_name = name,
            NativeBuildRange = 20f,
            gameObject = new UnityEngine.GameObject(prefab),
            m_areaMarker = new UnityEngine.GameObject($"{prefab}_marker"),
            transform = new UnityEngine.Transform { position = Point(x, y) }
        };
        station.m_areaMarker.AddComponent(new CircleProjector { m_radius = 20f });
        return station;
    }

    private static UnityEngine.Vector3 Point(float x, float y = 0f)
    {
        return new UnityEngine.Vector3(x, y, 0f);
    }

    private static void VerifyTranspilerShape(Type patchType, string replacementName)
    {
        MethodInfo nativeLookup = typeof(CraftingStation).GetMethod(nameof(CraftingStation.HaveBuildStationInRange))!;
        MethodInfo placementLookup = typeof(StationBuildCoverage).GetMethod(
            replacementName,
            BindingFlags.NonPublic | BindingFlags.Static)!;
        CodeInstruction nativeCall = new CodeInstruction(OpCodes.Call, nativeLookup);
        List<CodeInstruction> output = Invoke(patchType, Frame(nativeCall));
        Expect(output.Count == 3);
        Expect(output[1] == nativeCall);
        Expect(nativeCall.opcode == OpCodes.Call);
        Expect(Equals(nativeCall.operand, placementLookup));
        Expect(nativeCall.labels.Count == 1);
        Expect(nativeCall.blocks.Count == 1);

        ExpectThrows(() => Invoke(patchType, Frame(new CodeInstruction(OpCodes.Nop))));
        ExpectThrows(() => Invoke(patchType, Frame(
            new CodeInstruction(OpCodes.Call, nativeLookup),
            new CodeInstruction(OpCodes.Call, nativeLookup))));
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

    private static List<CodeInstruction> Invoke(Type patchType, IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo transpiler = patchType.GetMethod(
            "Transpiler",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Missing {patchType.Name}.Transpiler.");
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
        throw new InvalidOperationException("Expected the station coverage transpiler to reject a changed native seam.");
    }

    private static void Expect(bool condition)
    {
        if (!condition)
        {
            throw new InvalidOperationException("Station build coverage assertion failed.");
        }
    }
}
