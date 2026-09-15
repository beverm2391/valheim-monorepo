using System;
using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.Woodcutting;

internal static class FinewoodDrops
{
    private const string WoodPrefab = "Wood";
    private const string FinewoodPrefab = "FineWood";
    private const string CorewoodPrefab = "RoundLog";

    // Exact vanilla prefab identities keep modded or similarly named logs unchanged.
    private static readonly HashSet<string> FinewoodLogPrefabs = new HashSet<string>(
        new[]
        {
            "Birch_log",
            "Birch_log_half",
            "Oak_log",
            "Oak_log_half"
        },
        StringComparer.Ordinal);

    private static readonly HashSet<string> CorewoodLogPrefabs = new HashSet<string>(
        new[]
        {
            "PineTree_log",
            "PineTree_log_half"
        },
        StringComparer.Ordinal);

    internal static GameObject? ConvertNativeWood(GameObject? drop, TreeLog log)
    {
        if (drop == null || Utils.GetPrefabName(drop) != WoodPrefab)
        {
            return drop;
        }

        string logPrefab = Utils.GetPrefabName(log.gameObject);
        string? specialtyWoodPrefab = FinewoodLogPrefabs.Contains(logPrefab)
            ? FinewoodPrefab
            : CorewoodLogPrefabs.Contains(logPrefab)
                ? CorewoodPrefab
                : null;

        GameObject? specialtyWood = specialtyWoodPrefab == null
            ? null
            : ObjectDB.instance?.GetItemPrefab(specialtyWoodPrefab);
        return specialtyWood ?? drop;
    }
}
