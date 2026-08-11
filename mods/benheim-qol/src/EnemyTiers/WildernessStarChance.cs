using System;

namespace BenheimQoL.EnemyTiers;

internal static class WildernessStarChance
{
    internal const float MaxChancePercent = 30f;
    internal const float WorldEdgeBonus = 0.75f;

    internal static float NormalizeDistance(float distanceFromWorldCenter, float worldSize)
    {
        if (worldSize <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(worldSize));
        }

        return MathF.Max(0f, MathF.Min(distanceFromWorldCenter / worldSize, 1f));
    }

    internal static float DistanceMultiplier(float normalizedDistance)
    {
        normalizedDistance = MathF.Max(0f, MathF.Min(normalizedDistance, 1f));
        return 1f + (WorldEdgeBonus * normalizedDistance);
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

        // An authored or native modifier above Benheim's cap remains authoritative.
        if (nativeEffectiveChance > MaxChancePercent)
        {
            return nativeEffectiveChance;
        }

        return ComposeChance(biomeCurve, distanceFromWorldCenter, worldSize);
    }

    internal static float ComposeChance(
        BiomeChanceCurve biomeCurve,
        float distanceFromWorldCenter,
        float worldSize)
    {
        float normalizedDistance = NormalizeDistance(distanceFromWorldCenter, worldSize);
        float adjustedChance = biomeCurve.ChanceAt(normalizedDistance)
            * DistanceMultiplier(normalizedDistance);
        return MathF.Min(adjustedChance, MaxChancePercent);
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
