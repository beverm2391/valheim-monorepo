namespace BenheimQoL.EnemyTiers;

internal readonly struct HoveredDanger
{
    internal HoveredDanger(
        Heightmap.Biome biome,
        float distance,
        float normalizedDistance,
        float chance,
        bool locallyExplored,
        bool sharedExplored,
        bool showSharedMapData,
        WildernessDanger danger)
    {
        Biome = biome;
        Distance = distance;
        NormalizedDistance = normalizedDistance;
        Chance = chance;
        LocallyExplored = locallyExplored;
        SharedExplored = sharedExplored;
        ShowSharedMapData = showSharedMapData;
        Danger = danger;
    }

    internal Heightmap.Biome Biome { get; }
    internal float Distance { get; }
    internal float NormalizedDistance { get; }
    internal float Chance { get; }
    internal bool LocallyExplored { get; }
    internal bool SharedExplored { get; }
    internal bool ShowSharedMapData { get; }
    internal WildernessDanger Danger { get; }
}

internal enum HoverProbeStage
{
    PatchInvoked,
    NotLargeMap,
    LargeMapReady,
    WorldGeneratorMissing,
    NativeBiomeLabelEmpty,
    NativeBiomeLabelUnresolved,
    LocalPointRejected,
    BoundsRejected,
    ExplorationArrayRejected,
}
