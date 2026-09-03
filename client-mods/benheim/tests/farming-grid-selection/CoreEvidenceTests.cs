using System;
using System.Linq;
using System.Text.Json;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using UnityEngine;

internal static class CoreEvidenceTests
{
    internal static void Run()
    {
        Diagnostics.CoreEvents.Clear();
        PlantingDiagnostics.ResetPreview();
        Time.realtimeSinceStartup = 0;
        string[] cells = Enumerable.Repeat("valid", 9).ToArray();
        cells[4] = "anchor";
        PlantingDiagnostics.Preview("Carrot", 3, 1f, cells);
        Require(Last().GetProperty("grid_size").GetInt32() == 3 &&
            Last().GetProperty("cells").GetInt32() == 9 &&
            Last().GetProperty("valid").GetInt32() == 8,
            "preview evidence describes the consumed grid and excludes its native anchor from extra validity");
        cells[0] = "blocked_grow_space";
        for (int frame = 0; frame < 60; frame++) PlantingDiagnostics.Preview("Carrot", 3, 1f, cells);
        Require(Diagnostics.CoreEvents.Count == 1, "preview movement coalesces within the reporting interval");
        Time.realtimeSinceStartup = 1.1f;
        PlantingDiagnostics.Preview("Carrot", 3, 1f, cells);
        Require(Last().GetProperty("invalid").GetInt32() == 1 &&
            Last().GetProperty("cell_reasons").GetString()!.StartsWith("blocked_grow_space,", StringComparison.Ordinal),
            "a changed preview retains its reason and cell identity in one record");
        Time.realtimeSinceStartup = 10f;
        PlantingDiagnostics.Preview("Carrot", 3, 1f, cells);
        Require(Diagnostics.CoreEvents.Count == 2, "unchanged preview has no heartbeat spam");
        PlantingDiagnostics.Preview("Carrot", 5, 1f, Enumerable.Repeat("valid", 25).ToArray());
        Require(Diagnostics.CoreEvents.Count == 3 && Last().GetProperty("grid_size").GetInt32() == 5,
            "changing the selected grid bypasses preview coalescing");
        PlantingDiagnostics.PlacementFinished(5, 7, 2, 3, "insufficient_resources", 13);
        Require(Last().GetProperty("grid_size").GetInt32() == 5 &&
            Last().GetProperty("planted").GetInt32() == 8 &&
            Last().GetProperty("stopped_index").GetInt32() == 13,
            "placement evidence keeps the captured grid, completed placements, and stopping position");

    }

    private static JsonElement Last() => JsonDocument.Parse(Diagnostics.CoreEvents.Last().ToJsonLine()).RootElement;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
