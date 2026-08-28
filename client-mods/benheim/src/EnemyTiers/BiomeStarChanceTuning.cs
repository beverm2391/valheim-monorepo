namespace BenheimQoL.EnemyTiers;

// This is the one tuning owner for ordinary wilderness biome curves. Every
// listed biome interpolates over the same normalized distance from world
// center.
internal static class BiomeStarChanceTuning
{
    internal static bool TryGetCurve(Heightmap.Biome biome, out BiomeChanceCurve curve)
    {
        switch (biome)
        {
        case Heightmap.Biome.Meadows:
            curve = new BiomeChanceCurve(10f, 12f);
            return true;
        case Heightmap.Biome.BlackForest:
            curve = new BiomeChanceCurve(10f, 18f);
            return true;
        case Heightmap.Biome.Swamp:
            curve = new BiomeChanceCurve(12f, 22f);
            return true;
        case Heightmap.Biome.Mountain:
            curve = new BiomeChanceCurve(14f, 24f);
            return true;
        case Heightmap.Biome.Plains:
            curve = new BiomeChanceCurve(16f, 27f);
            return true;
        case Heightmap.Biome.Mistlands:
            curve = new BiomeChanceCurve(18f, 30f);
            return true;
        default:
            curve = default;
            return false;
        }
    }
}
