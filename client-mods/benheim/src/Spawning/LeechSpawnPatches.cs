using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Spawning;

[HarmonyPatch]
internal static class LeechSpawnPatches
{
    private static readonly LeechSpawnAdjustmentState<SpawnSystem.SpawnData> Adjusted = new();
    private static readonly List<SpawnSystem> PendingSpawnSystems = new();
    private static GameObject? leechPrefab;
    private static bool adjustmentLogged;
    private static bool failureLogged;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(SpawnSystem), "Awake")]
    private static void SpawnSystemAwakePostfix(SpawnSystem __instance)
    {
        if (ZNetScene.instance == null)
        {
            if (!PendingSpawnSystems.Contains(__instance))
            {
                PendingSpawnSystems.Add(__instance);
            }
            return;
        }

        Adjust(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ZNetScene), "Awake")]
    private static void ZNetSceneAwakePostfix()
    {
        foreach (SpawnSystem spawnSystem in PendingSpawnSystems)
        {
            if (spawnSystem)
            {
                Adjust(spawnSystem);
            }
        }

        PendingSpawnSystems.Clear();
    }

    private static void Adjust(SpawnSystem spawnSystem)
    {
        GameObject? prefab = ResolveLeechPrefab();
        if (prefab == null)
        {
            LogFailureOnce("leech_prefab_missing");
            return;
        }

        if (spawnSystem.m_spawnLists == null)
        {
            LogFailureOnce("spawn_lists_missing");
            return;
        }

        // These serialized lists are the ordinary base-world path. Event
        // SpawnData arrives separately and never enters this seam.
        foreach (SpawnSystemList spawnList in spawnSystem.m_spawnLists)
        {
            if (spawnList == null || spawnList.m_spawners == null)
            {
                continue;
            }

            foreach (SpawnSystem.SpawnData spawner in spawnList.m_spawners)
            {
                if (spawner == null || spawner.m_prefab != prefab || !Adjusted.TryClaim(spawner))
                {
                    continue;
                }

                float nativeInterval = spawner.m_spawnInterval;
                spawner.m_spawnInterval = LeechSpawnFrequency.AdjustInterval(nativeInterval);
                LogAdjustmentOnce(nativeInterval, spawner.m_spawnInterval);
            }
        }
    }

    private static void LogFailureOnce(string reason)
    {
        if (failureLogged)
        {
            return;
        }

        failureLogged = true;
        Plugin.Log.LogError($"Benheim could not adjust Leech spawning: {reason}");
        Diagnostics.Event(
            "Spawning",
            "leech_interval_failed",
            $"reason={reason}");
    }

    private static GameObject? ResolveLeechPrefab()
    {
        if (leechPrefab != null)
        {
            return leechPrefab;
        }

        ZNetScene? scene = ZNetScene.instance;
        if (scene == null)
        {
            return null;
        }

        leechPrefab = scene.GetPrefab(LeechSpawnFrequency.PrefabName);
        return leechPrefab;
    }

    private static void LogAdjustmentOnce(float nativeInterval, float adjustedInterval)
    {
        if (adjustmentLogged)
        {
            return;
        }

        adjustmentLogged = true;
        Diagnostics.Event(
            "Spawning",
            "leech_interval_adjusted",
            $"source=base_world prefab={LeechSpawnFrequency.PrefabName} " +
            $"factor={LeechSpawnFrequency.OpportunityMultiplier:0.###} " +
            $"native_interval={nativeInterval:0.###} adjusted_interval={adjustedInterval:0.###}");
    }
}
