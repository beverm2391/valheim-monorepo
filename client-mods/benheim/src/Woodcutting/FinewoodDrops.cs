using System;
using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.Woodcutting;

internal static class FinewoodDrops
{
    private const string WoodPrefab = "Wood";
    private const string FinewoodPrefab = "FineWood";

    private static readonly HashSet<string> FinewoodLogPrefabs = new HashSet<string>(
        new[]
        {
            "Birch_log",
            "Birch_log_half",
            "Oak_log",
            "Oak_log_half",
            "PineTree_log",
            "PineTree_log_half"
        },
        StringComparer.Ordinal);

    internal static GameObject? ConvertNativeWood(GameObject? drop, TreeLog log)
    {
        if (!FinewoodLogPrefabs.Contains(Utils.GetPrefabName(log.gameObject))
            || drop == null
            || Utils.GetPrefabName(drop) != WoodPrefab)
        {
            return drop;
        }

        GameObject? finewood = ObjectDB.instance?.GetItemPrefab(FinewoodPrefab);
        return finewood ?? drop;
    }
}
