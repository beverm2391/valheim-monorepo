using System;
using System.Collections.Generic;
using System.Linq;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

internal static class PlantingDiagnostics
{
    private static string lastPreview = string.Empty;
    private static string lastPrefab = string.Empty;
    private static int lastSize;
    private static float nextPreviewTime;

    internal static void ResetPreview()
    {
        lastPreview = lastPrefab = string.Empty;
        lastSize = 0;
        nextPreviewTime = 0;
    }

    internal static void Preview(string prefab, int size, float spacing, IReadOnlyList<string> reasons)
    {
        // Preserve the per-cell reason map in one bounded record. Coalesce
        // moving-preview changes to at most once per second, while a new
        // prefab or selected size is immediately observable.
        string signature = string.Join(",", reasons);
        bool newGrid = lastPrefab != prefab || lastSize != size;
        if (!newGrid && (signature == lastPreview || Time.realtimeSinceStartup < nextPreviewTime)) return;
        lastPreview = signature;
        lastPrefab = prefab;
        lastSize = size;
        nextPreviewTime = Time.realtimeSinceStartup + 1f;
        int valid = reasons.Count(reason => reason == "valid");
        int anchor = reasons.Count(reason => reason == "anchor");
        Emit(DiagnosticEvent.Create("Farming", "plant_preview_updated")
            .String("prefab", prefab)
            .Integer("grid_size", size)
            .Number("spacing", spacing)
            .Integer("cells", reasons.Count)
            .Integer("valid", valid)
            .Integer("invalid", reasons.Count - valid - anchor)
            .String("cell_reasons", signature));
    }

    internal static void PlacementFinished(
        int size, int extraPlanted, int notCultivated, int blocked,
        string reason = "complete", int stoppedIndex = -1)
    {
        Emit(DiagnosticEvent.Create("Farming", "mass_plant_finished")
            .Integer("grid_size", size)
            .Integer("planted", extraPlanted + 1)
            .Integer("extra_planted", extraPlanted)
            .Integer("skipped_not_cultivated", notCultivated)
            .Integer("skipped_blocked", blocked)
            .String("reason", reason)
            .Integer("stopped_index", stoppedIndex));
    }

    private static void Emit(DiagnosticEvent evidence)
    {
        // Diagnostics cannot interrupt resource/stamina handling after a native
        // placement. The normal sink reports its own transport failures.
        try { Diagnostics.Emit(evidence); }
        catch (Exception) { }
    }
}
