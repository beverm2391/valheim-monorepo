namespace BenheimQoL.EnemyTiers;

/// <summary>
/// One factual player-area reading. Keeping the biome and resolved danger in
/// one value prevents the minimap from combining observations from two frames.
/// </summary>
internal readonly struct WildernessPlayerArea
{
    private WildernessPlayerArea(
        Heightmap.Biome biome,
        float distance,
        float distanceRatio,
        float adjustedChance,
        WildernessDanger? danger)
    {
        Biome = biome;
        Distance = distance;
        DistanceRatio = distanceRatio;
        AdjustedChance = adjustedChance;
        Danger = danger;
    }

    internal Heightmap.Biome Biome { get; }
    internal float Distance { get; }
    internal float DistanceRatio { get; }
    internal float AdjustedChance { get; }
    internal WildernessDanger? Danger { get; }

    internal static WildernessPlayerArea Tuned(
        Heightmap.Biome biome,
        float distance,
        float worldSize,
        float adjustedChance)
    {
        return new WildernessPlayerArea(
            biome,
            distance,
            WildernessStarChance.NormalizeDistance(distance, worldSize),
            adjustedChance,
            WildernessDangerScale.Classify(adjustedChance));
    }

    internal static WildernessPlayerArea Untuned(
        Heightmap.Biome biome,
        float distance,
        float worldSize)
    {
        return new WildernessPlayerArea(
            biome,
            distance,
            WildernessStarChance.NormalizeDistance(distance, worldSize),
            0f,
            null);
    }
}
