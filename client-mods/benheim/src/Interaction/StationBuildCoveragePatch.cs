using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Interaction;

internal static class StationBuildCoverage
{
    internal const float Multiplier = 2f;
    internal const string WorkbenchName = "$piece_workbench";
    internal const string StonecutterName = "$piece_stonecutter";
    internal const string WorkbenchPrefab = "piece_workbench";
    internal const string StonecutterPrefab = "piece_stonecutter";

    private static readonly List<CraftingStation> AllStations = ResolveAllStations();

    internal static CraftingStation? FindForPlacement(string name, Vector3 point)
    {
        CraftingStation? nativeStation = CraftingStation.HaveBuildStationInRange(name, point);
        if (nativeStation != null || !TryGetNativePrefab(name, out string prefabName))
        {
            return nativeStation;
        }

        foreach (CraftingStation station in AllStations)
        {
            if (station.m_name != name || Utils.GetPrefabName(station.gameObject) != prefabName)
            {
                continue;
            }

            Vector3 horizontalPoint = point;
            horizontalPoint.y = station.transform.position.y;
            float extendedRange = station.GetStationBuildRange() * Multiplier;
            if (Vector3.Distance(station.transform.position, horizontalPoint) < extendedRange)
            {
                return station;
            }
        }

        return null;
    }

    private static bool TryGetNativePrefab(string stationName, out string prefabName)
    {
        if (stationName == WorkbenchName)
        {
            prefabName = WorkbenchPrefab;
            return true;
        }

        if (stationName == StonecutterName)
        {
            prefabName = StonecutterPrefab;
            return true;
        }

        prefabName = "";
        return false;
    }

    private static List<CraftingStation> ResolveAllStations()
    {
        FieldInfo field = AccessTools.Field(typeof(CraftingStation), "m_allStations")
            ?? throw new MissingFieldException(typeof(CraftingStation).FullName, "m_allStations");
        return field.GetValue(null) as List<CraftingStation>
            ?? throw new InvalidOperationException("Valheim's crafting-station registry is unavailable.");
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[]
{
    typeof(Piece),
    typeof(Player.RequirementMode)
})]
internal static class StationBuildCoveragePatch
{
    private static readonly MethodInfo NativeLookup = AccessTools.Method(
        typeof(CraftingStation),
        nameof(CraftingStation.HaveBuildStationInRange))
        ?? throw new MissingMethodException(
            typeof(CraftingStation).FullName,
            nameof(CraftingStation.HaveBuildStationInRange));

    private static readonly MethodInfo PlacementLookup = AccessTools.Method(
        typeof(StationBuildCoverage),
        nameof(StationBuildCoverage.FindForPlacement))
        ?? throw new MissingMethodException(
            typeof(StationBuildCoverage).FullName,
            nameof(StationBuildCoverage.FindForPlacement));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        List<CodeInstruction> output = new(instructions);
        int replacements = 0;

        foreach (CodeInstruction instruction in output)
        {
            if (!instruction.Calls(NativeLookup))
            {
                continue;
            }

            instruction.opcode = System.Reflection.Emit.OpCodes.Call;
            instruction.operand = PlacementLookup;
            replacements++;
        }

        if (replacements != 1)
        {
            throw new InvalidOperationException(
                $"Expected one native station lookup in Player.HaveRequirements, found {replacements}.");
        }

        return output;
    }
}
