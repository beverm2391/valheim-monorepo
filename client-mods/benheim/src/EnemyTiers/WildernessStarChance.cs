using System;

namespace BenheimQoL.EnemyTiers;

internal static class WildernessStarChance
{
    internal const float WorldEdgeAdditionPercent = 10f;

    internal static float NormalizeDistance(float distanceFromWorldCenter, float worldSize)
    {
        if (worldSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(worldSize));
        }

        return MathF.Max(0f, MathF.Min(distanceFromWorldCenter / worldSize, 1f));
    }

    internal static float GlobalDistanceAddition(float normalizedDistance)
    {
        normalizedDistance = MathF.Max(0f, MathF.Min(normalizedDistance, 1f));
        return WorldEdgeAdditionPercent * normalizedDistance;
    }

    internal static float AdjustEffectiveChance(
        float nativeChance,
        float nativeLevelUpMultiplier,
        BiomeChanceCurve biomeCurve,
        float distanceFromWorldCenter,
        float worldSize)
    {
        float nativeEffectiveChance = nativeChance * nativeLevelUpMultiplier;
        if (nativeEffectiveChance <= 0f)
        {
            return 0f;
        }

        return ComposeChance(biomeCurve, distanceFromWorldCenter, worldSize);
    }

    internal static float ComposeChance(
        BiomeChanceCurve biomeCurve,
        float distanceFromWorldCenter,
        float worldSize)
    {
        float normalizedDistance = NormalizeDistance(distanceFromWorldCenter, worldSize);
        return biomeCurve.ChanceAt(normalizedDistance)
            + GlobalDistanceAddition(normalizedDistance);
    }

    internal static bool ShouldAdjust(bool eventSpawner, bool inInterior, bool hasBiomeTuning)
    {
        return !eventSpawner && !inInterior && hasBiomeTuning;
    }
}

internal readonly struct BiomeChanceCurve
{
    internal BiomeChanceCurve(float minimumChance, float maximumChance)
    {
        if (minimumChance < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumChance));
        }

        if (maximumChance < minimumChance)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumChance));
        }

        MinimumChance = minimumChance;
        MaximumChance = maximumChance;
    }

    internal float MinimumChance { get; }
    internal float MaximumChance { get; }

    internal float ChanceAt(float normalizedDistance)
    {
        normalizedDistance = MathF.Max(0f, MathF.Min(normalizedDistance, 1f));
        return MinimumChance + ((MaximumChance - MinimumChance) * normalizedDistance);
    }
}
