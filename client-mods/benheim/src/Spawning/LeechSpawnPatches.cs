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

    [HarmonyPatch(
        typeof(SpawnSystem),
        "Spawn",
        typeof(SpawnSystem.SpawnData),
        typeof(Vector3),
        typeof(bool),
        typeof(int),
        typeof(float))]
    private static class SuccessfulSpawnPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            SpawnSystem.SpawnData critter,
            bool eventSpawner,
            out bool __state)
        {
            // Spawn() has one normal early return: the global no-spawn flag.
            // A postfix with this state therefore runs only after the adjusted
            // ordinary SpawnData completed native instantiation and setup.
            __state = !SpawnSystem.m_nospawn &&
                !eventSpawner &&
                Adjusted.Contains(critter);
        }

        [HarmonyPostfix]
        private static void Postfix(SpawnSystem.SpawnData critter, bool __state)
        {
            if (!__state)
            {
                return;
            }

            Diagnostics.Emit(
                DiagnosticEvent.Create("Spawning", "leech_spawn_succeeded")
                    .String("source", "base_world")
                    .String("prefab", critter.m_prefab.name)
                    .Number("opportunity_multiplier", LeechSpawnFrequency.OpportunityMultiplier));
        }
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
                if (spawner == null || spawner.m_prefab != prefab)
                {
                    continue;
                }

                if (Adjusted.TryClaim(spawner))
                {
                    float nativeInterval = spawner.m_spawnInterval;
                    spawner.m_spawnInterval = LeechSpawnFrequency.AdjustInterval(nativeInterval);
                    LogAdjustmentOnce(nativeInterval, spawner.m_spawnInterval);
                }

                // Registration is a lifecycle operation, not part of the
                // one-time mutation. A world transition clears probe targets,
                // so an already-adjusted native rule must be able to register
                // again without applying the interval multiplier twice.
                SpawnPopulationProbe.RegisterRule(
                    "base_world",
                    LeechSpawnFrequency.PrefabName,
                    spawner);
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
