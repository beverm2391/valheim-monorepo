using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

internal static class WildernessStarPatches
{
    [ThreadStatic]
    private static SpawnContext? activeSpawn;

    private static readonly HashSet<string> LoggedDistanceWindows = new();

    [HarmonyPatch(
        typeof(SpawnSystem),
        "Spawn",
        typeof(SpawnSystem.SpawnData),
        typeof(Vector3),
        typeof(bool))]
    [PatchGroup("EnemyTiers.Spawn")]
    private static class SpawnPatch
    {
        [HarmonyPrefix]
        private static void Prefix(
            SpawnSystem.SpawnData critter,
            Vector3 spawnPoint,
            bool eventSpawner,
            out SpawnContext? __state)
        {
            __state = activeSpawn;
            activeSpawn = null;

            bool inInterior = Character.InInterior(spawnPoint);
            ZoneSystem? zoneSystem = ZoneSystem.instance;
            bool hasBiomeTuning = false;
            Heightmap.Biome biome = Heightmap.Biome.None;
            BiomeChanceCurve biomeCurve = default;
            if (zoneSystem != null)
            {
                // Match SpawnSystem.IsSpawnPointGood(): sample the biome from
                // the loaded Heightmap at the accepted point, not from the
                // SpawnData biome mask or a player position.
                Vector3 biomePoint = spawnPoint;
                zoneSystem.GetGroundData(
                    ref biomePoint,
                    out _,
                    out biome,
                    out _,
                    out _);
                hasBiomeTuning = BiomeStarChanceTuning.TryGetCurve(biome, out biomeCurve);
            }

            if (!WildernessStarChance.ShouldAdjust(eventSpawner, inInterior, hasBiomeTuning))
            {
                return;
            }

            activeSpawn = new SpawnContext(
                critter,
                biome,
                biomeCurve,
                Utils.LengthXZ(spawnPoint));
        }

        [HarmonyFinalizer]
        private static Exception? Finalizer(Exception? __exception, SpawnContext? __state)
        {
            activeSpawn = __state;
            return __exception;
        }
    }

    [HarmonyPatch(
        typeof(SpawnSystem),
        nameof(SpawnSystem.GetLevelUpChance),
        typeof(Vector3),
        typeof(SpawnSystem.SpawnData))]
    [PatchGroup("EnemyTiers.Spawn")]
    private static class LevelUpChancePatch
    {
        [HarmonyPostfix]
        private static void Postfix(Vector3 position, SpawnSystem.SpawnData creature, ref float __result)
        {
            if (!activeSpawn.HasValue || !ReferenceEquals(activeSpawn.Value.SpawnData, creature))
            {
                return;
            }

            SpawnContext context = activeSpawn.Value;
            float nativeEffectiveChance = __result;
            float adjustedEffectiveChance = WildernessStarChance.AdjustEffectiveChance(
                nativeEffectiveChance,
                context.BiomeCurve,
                context.DistanceFromWorldCenter,
                WorldGenerator.worldSize);
            __result = adjustedEffectiveChance;
            LogAdjustment(context, nativeEffectiveChance, adjustedEffectiveChance);
        }
    }

    private static void LogAdjustment(
        SpawnContext context,
        float nativeEffectiveChance,
        float adjustedEffectiveChance)
    {
        float normalizedDistance = WildernessStarChance.NormalizeDistance(
            context.DistanceFromWorldCenter,
            WorldGenerator.worldSize);
        int distanceWindow = (int)MathF.Floor(normalizedDistance * 10f);
        string key = $"{context.Biome}:{distanceWindow}";
        if (!LoggedDistanceWindows.Add(key))
        {
            return;
        }

        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_star_chance",
            $"source=ordinary_wilderness biome={context.Biome} " +
            $"distance={context.DistanceFromWorldCenter:0} " +
            $"distance_ratio={normalizedDistance:0.###} " +
            $"biome_min_chance={context.BiomeCurve.MinimumChance:0.###} " +
            $"biome_max_chance={context.BiomeCurve.MaximumChance:0.###} " +
            $"biome_chance={context.BiomeCurve.ChanceAt(normalizedDistance):0.###} " +
            $"global_distance_addition={WildernessStarChance.GlobalDistanceAddition(normalizedDistance):0.###} " +
            $"native_chance={nativeEffectiveChance:0.###} " +
            $"adjusted_chance={adjustedEffectiveChance:0.###}");
    }

    private readonly struct SpawnContext
    {
        internal SpawnContext(
            SpawnSystem.SpawnData spawnData,
            Heightmap.Biome biome,
            BiomeChanceCurve biomeCurve,
            float distanceFromWorldCenter)
        {
            SpawnData = spawnData;
            Biome = biome;
            BiomeCurve = biomeCurve;
            DistanceFromWorldCenter = distanceFromWorldCenter;
        }

        internal SpawnSystem.SpawnData SpawnData { get; }
        internal Heightmap.Biome Biome { get; }
        internal BiomeChanceCurve BiomeCurve { get; }
        internal float DistanceFromWorldCenter { get; }
    }
}
