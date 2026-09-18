using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
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
        Resolution resolution = Resolve(name, point);
        StationBuildCoverageDiagnostics.Observe("placement", name, resolution.Result);
        return resolution.Station;
    }

    internal static CraftingStation? FindForMaintenance(string name, Vector3 point)
    {
        Resolution resolution = Resolve(name, point);
        StationBuildCoverageDiagnostics.Observe("repair_or_dismantle", name, resolution.Result);
        return resolution.Station;
    }

    internal static CraftingStation? FindForHammerUi(string name, Vector3 point)
    {
        Resolution resolution = Resolve(name, point);
        if (resolution.Station != null && resolution.ExtendedRange > 0f)
        {
            SetAreaMarkerRadius(resolution.Station, resolution.ExtendedRange);
        }
        StationBuildCoverageDiagnostics.Observe("hammer_ui", name, resolution.Result);
        return resolution.Station;
    }

    private static Resolution Resolve(string name, Vector3 point)
    {
        CraftingStation? nativeStation = CraftingStation.HaveBuildStationInRange(name, point);
        if (nativeStation != null)
        {
            if (!TryGetNativePrefab(name, out string nativePrefab)
                || Utils.GetPrefabName(nativeStation.gameObject) != nativePrefab)
            {
                return new Resolution(nativeStation, "native", 0f);
            }

            return new Resolution(
                nativeStation,
                "native",
                nativeStation.GetStationBuildRange() * Multiplier);
        }

        if (!TryGetNativePrefab(name, out string prefabName))
        {
            return new Resolution(null, "missing", 0f);
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
                return new Resolution(station, "extended", extendedRange);
            }
        }

        return new Resolution(null, "missing", 0f);
    }

    private static void SetAreaMarkerRadius(CraftingStation station, float radius)
    {
        // The projector is visual only. Valheim keeps Workbench suppression on
        // m_effectAreaCollider, so changing only this component makes the
        // Hammer boundary agree without widening any station world effect.
        CircleProjector? projector = station.m_areaMarker?.GetComponent<CircleProjector>();
        if (projector != null)
        {
            projector.m_radius = radius;
        }
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

    private readonly struct Resolution
    {
        internal Resolution(CraftingStation? station, string result, float extendedRange)
        {
            Station = station;
            Result = result;
            ExtendedRange = extendedRange;
        }

        internal CraftingStation? Station { get; }
        internal string Result { get; }
        internal float ExtendedRange { get; }
    }
}

internal static class StationBuildCoverageDiagnostics
{
    private static readonly Dictionary<string, string> LastResults = new(StringComparer.Ordinal);

    internal static void Observe(string action, string station, string result)
    {
        string key = $"{action}:{station}";
        if (LastResults.TryGetValue(key, out string? previous) && previous == result)
        {
            return;
        }
        LastResults[key] = result;
        Emit(action, station, result);
    }

    private static void Emit(string action, string station, string result)
    {
        if (station != StationBuildCoverage.WorkbenchName
            && station != StationBuildCoverage.StonecutterName)
        {
            return;
        }

        try
        {
            Diagnostics.Emit(DiagnosticEvent.Create("Interaction", "hammer_station_range_state")
                .String("action", action)
                .String("station", station)
                .String("result", result)
                .Boolean("in_range", result != "missing")
                .Number("range_multiplier", StationBuildCoverage.Multiplier));
        }
        catch (Exception)
        {
            // Diagnostics must not interrupt Valheim's native Hammer path.
        }
    }
}

internal static class StationBuildCoverageTranspiler
{
    private static readonly MethodInfo NativeLookup = AccessTools.Method(
        typeof(CraftingStation),
        nameof(CraftingStation.HaveBuildStationInRange))
        ?? throw new MissingMethodException(
            typeof(CraftingStation).FullName,
            nameof(CraftingStation.HaveBuildStationInRange));

    internal static IEnumerable<CodeInstruction> Replace(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo replacement,
        string seam)
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
            instruction.operand = replacement;
            replacements++;
        }

        if (replacements != 1)
        {
            throw new InvalidOperationException(
                $"Expected one native station lookup in {seam}, found {replacements}.");
        }

        return output;
    }
}

[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[]
{
    typeof(Piece),
    typeof(Player.RequirementMode)
})]
internal static class StationBuildPlacementCoveragePatch
{
    private static readonly MethodInfo Replacement = AccessTools.Method(
        typeof(StationBuildCoverage),
        nameof(StationBuildCoverage.FindForPlacement))
        ?? throw new MissingMethodException(
            typeof(StationBuildCoverage).FullName,
            nameof(StationBuildCoverage.FindForPlacement));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return StationBuildCoverageTranspiler.Replace(
            instructions,
            Replacement,
            "Player.HaveRequirements(Piece, RequirementMode)");
    }
}

[HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
internal static class StationBuildMaintenanceCoveragePatch
{
    private static readonly MethodInfo Replacement = AccessTools.Method(
        typeof(StationBuildCoverage),
        nameof(StationBuildCoverage.FindForMaintenance))
        ?? throw new MissingMethodException(
            typeof(StationBuildCoverage).FullName,
            nameof(StationBuildCoverage.FindForMaintenance));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return StationBuildCoverageTranspiler.Replace(
            instructions,
            Replacement,
            "Player.CheckCanRemovePiece");
    }
}

[HarmonyPatch(typeof(Hud), "SetupPieceInfo")]
internal static class StationBuildUiCoveragePatch
{
    private static readonly MethodInfo Replacement = AccessTools.Method(
        typeof(StationBuildCoverage),
        nameof(StationBuildCoverage.FindForHammerUi))
        ?? throw new MissingMethodException(
            typeof(StationBuildCoverage).FullName,
            nameof(StationBuildCoverage.FindForHammerUi));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return StationBuildCoverageTranspiler.Replace(
            instructions,
            Replacement,
            "Hud.SetupPieceInfo");
    }
}
