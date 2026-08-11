using System;
using BenheimQoL.EnemyTiers;

const float WorldSize = 10_000f;
var exampleBiome = new BiomeChanceCurve(minimumChance: 8f, maximumChance: 16f);

ExpectClose(0f, WildernessStarChance.NormalizeDistance(0f, WorldSize), "center normalized distance");
ExpectClose(0.5f, WildernessStarChance.NormalizeDistance(5_000f, WorldSize), "midpoint normalized distance");
ExpectClose(1f, WildernessStarChance.NormalizeDistance(WorldSize, WorldSize), "world-edge normalized distance");
ExpectClose(0f, WildernessStarChance.NormalizeDistance(-100f, WorldSize), "negative distance clamps to center");
ExpectClose(1f, WildernessStarChance.NormalizeDistance(12_000f, WorldSize), "distance beyond world edge clamps");

ExpectClose(1f, WildernessStarChance.DistanceMultiplier(0f), "center multiplier");
ExpectClose(1.375f, WildernessStarChance.DistanceMultiplier(0.5f), "midpoint multiplier");
ExpectClose(1.75f, WildernessStarChance.DistanceMultiplier(1f), "world-edge multiplier");
ExpectClose(1f, WildernessStarChance.DistanceMultiplier(-1f), "negative multiplier input clamps");
ExpectClose(1.75f, WildernessStarChance.DistanceMultiplier(2f), "multiplier input beyond edge clamps");

ExpectClose(8f, exampleBiome.ChanceAt(-1f), "biome curve clamps below center");
ExpectClose(8f, exampleBiome.ChanceAt(0f), "biome curve center");
ExpectClose(12f, exampleBiome.ChanceAt(0.5f), "biome curve midpoint");
ExpectClose(16f, exampleBiome.ChanceAt(1f), "biome curve world edge");
ExpectClose(16f, exampleBiome.ChanceAt(2f), "biome curve clamps beyond world edge");

ExpectClose(
    8f,
    WildernessStarChance.AdjustEffectiveChance(10f, 1f, exampleBiome, 0f, WorldSize),
    "biome minimum applies at center");
ExpectClose(
    16.5f,
    WildernessStarChance.AdjustEffectiveChance(10f, 1f, exampleBiome, 5_000f, WorldSize),
    "biome midpoint composes with global distance");
ExpectClose(
    28f,
    WildernessStarChance.AdjustEffectiveChance(10f, 1f, exampleBiome, WorldSize, WorldSize),
    "biome maximum composes with world-edge multiplier");

ExpectClose(
    1.369f,
    WildernessStarChance.DistanceMultiplier(WildernessStarChance.NormalizeDistance(4_920f, WorldSize)),
    "Bonemass-distance global calibration");
ExpectClose(
    1.59115f,
    WildernessStarChance.DistanceMultiplier(WildernessStarChance.NormalizeDistance(7_882f, WorldSize)),
    "known-frontier global calibration");

ExpectClose(
    0f,
    WildernessStarChance.AdjustEffectiveChance(0f, 1f, exampleBiome, WorldSize, WorldSize),
    "native zero chance remains zero");
ExpectClose(
    35f,
    WildernessStarChance.AdjustEffectiveChance(35f, 1f, exampleBiome, WorldSize, WorldSize),
    "native chance above cap is not reduced");
ExpectClose(
    28f,
    WildernessStarChance.AdjustEffectiveChance(10f, 2f, exampleBiome, WorldSize, WorldSize),
    "biome composition defines the final chance below the cap");

var cappedBiome = new BiomeChanceCurve(20f, 20f);
ExpectClose(30f, WildernessStarChance.AdjustEffectiveChance(10f, 1f, cappedBiome, WorldSize, WorldSize), "composed chance is capped");

ExpectTrue(WildernessDangerScale.Classify(0f) == WildernessDanger.Familiar, "zero pressure is familiar");
ExpectTrue(WildernessDangerScale.Classify(11.999f) == WildernessDanger.Familiar, "familiar upper edge");
ExpectTrue(WildernessDangerScale.Classify(12f) == WildernessDanger.Sketchy, "sketchy lower edge");
ExpectTrue(WildernessDangerScale.Classify(18f) == WildernessDanger.Dangerous, "dangerous lower edge");
ExpectTrue(WildernessDangerScale.Classify(24f) == WildernessDanger.Deadly, "deadly lower edge");
ExpectTrue(WildernessDangerScale.Label(WildernessDanger.Sketchy) == "Sketchy", "danger label is player-facing");
ExpectTrue(!WildernessDangerScale.IsVisible(false, false, false), "unexplored point stays hidden");
ExpectTrue(WildernessDangerScale.IsVisible(true, false, false), "locally explored point is visible");
ExpectTrue(!WildernessDangerScale.IsVisible(false, true, false), "hidden shared exploration stays hidden");
ExpectTrue(WildernessDangerScale.IsVisible(false, true, true), "enabled shared exploration is visible");

ExpectTrue(WildernessStarChance.ShouldAdjust(eventSpawner: false, inInterior: false, hasBiomeTuning: true), "ordinary tuned wilderness adjusts");
ExpectTrue(!WildernessStarChance.ShouldAdjust(eventSpawner: true, inInterior: false, hasBiomeTuning: true), "random-event spawn is excluded");
ExpectTrue(!WildernessStarChance.ShouldAdjust(eventSpawner: false, inInterior: true, hasBiomeTuning: true), "dungeon-height spawn is excluded");
ExpectTrue(!WildernessStarChance.ShouldAdjust(eventSpawner: false, inInterior: false, hasBiomeTuning: false), "untuned biome preserves native chance");

ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Meadows, out BiomeChanceCurve meadows), "Meadows tuning exists");
ExpectCurve(meadows, 10f, 12f, "Meadows starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.BlackForest, out BiomeChanceCurve blackForest), "Black Forest tuning exists");
ExpectCurve(blackForest, 10f, 18f, "Black Forest starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Swamp, out BiomeChanceCurve swamp), "Swamp tuning exists");
ExpectCurve(swamp, 12f, 22f, "Swamp starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Mountain, out BiomeChanceCurve mountain), "Mountain tuning exists");
ExpectCurve(mountain, 14f, 24f, "Mountain starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Plains, out BiomeChanceCurve plains), "Plains tuning exists");
ExpectCurve(plains, 16f, 27f, "Plains starter tuning");
ExpectTrue(BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Mistlands, out BiomeChanceCurve mistlands), "Mistlands tuning exists");
ExpectCurve(mistlands, 18f, 30f, "Mistlands starter tuning");
ExpectTrue(!BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.Ocean, out _), "Ocean stays native");
ExpectTrue(!BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.AshLands, out _), "Ashlands stays native");
ExpectTrue(!BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.DeepNorth, out _), "Deep North stays native");
ExpectTrue(!BiomeStarChanceTuning.TryGetCurve(Heightmap.Biome.None, out _), "non-biome input stays native");

ExpectClose(10f, WildernessStarChance.ComposeChance(meadows, 0f, WorldSize), "Meadows center chance");
ExpectClose(15.125f, WildernessStarChance.ComposeChance(meadows, 5_000f, WorldSize), "Meadows midpoint chance");
ExpectClose(21f, WildernessStarChance.ComposeChance(meadows, WorldSize, WorldSize), "Meadows world-edge chance");
ExpectClose(14.25f, WildernessStarChance.ComposeChance(blackForest, 2_500f, WorldSize), "Black Forest quarter-world chance");
ExpectClose(26.1f, WildernessStarChance.ComposeChance(swamp, 6_000f, WorldSize), "Swamp current-world distance chance");
ExpectClose(26.125f, WildernessStarChance.ComposeChance(mountain, 5_000f, WorldSize), "Mountain midpoint chance");
ExpectClose(30f, WildernessStarChance.ComposeChance(plains, 8_000f, WorldSize), "Plains remote chance caps");
ExpectClose(30f, WildernessStarChance.ComposeChance(mistlands, 6_000f, WorldSize), "Mistlands current-world distance caps");

ExpectThrows<ArgumentOutOfRangeException>(
    () => WildernessStarChance.NormalizeDistance(0f, 0f),
    "invalid world size is rejected");
ExpectThrows<ArgumentOutOfRangeException>(
    () => new BiomeChanceCurve(10f, 9f),
    "descending biome chance curve is rejected");

Console.WriteLine("enemy tier distance and scope checks passed");
return;

static void ExpectClose(float expected, float actual, string scenario)
{
    if (MathF.Abs(expected - actual) > 0.0001f)
    {
        throw new InvalidOperationException($"{scenario}: expected {expected}, got {actual}");
    }
}

static void ExpectTrue(bool value, string scenario)
{
    if (!value)
    {
        throw new InvalidOperationException($"{scenario}: expected true");
    }
}

static void ExpectCurve(BiomeChanceCurve curve, float minimumChance, float maximumChance, string scenario)
{
    ExpectClose(minimumChance, curve.MinimumChance, $"{scenario} minimum chance");
    ExpectClose(maximumChance, curve.MaximumChance, $"{scenario} maximum chance");
}

static void ExpectThrows<TException>(Action action, string scenario) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{scenario}: expected {typeof(TException).Name}");
}

internal static class Heightmap
{
    [Flags]
    internal enum Biome
    {
        None = 0,
        Meadows = 1,
        Swamp = 2,
        Mountain = 4,
        BlackForest = 8,
        Plains = 0x10,
        AshLands = 0x20,
        DeepNorth = 0x40,
        Ocean = 0x100,
        Mistlands = 0x200,
        All = 0x37F,
    }
}
