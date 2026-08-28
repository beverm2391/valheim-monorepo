namespace BenheimQoL.EnemyTiers;

// Runtime-only diagnostic suppression. This state is neither serialized nor
// consulted by the Boar profile, combat, AI, or networking paths.
internal sealed class BoarTierObservationState
{
    private int geometryLevels;
    private int playerHitLevels;

    internal bool HasGeometry(int level)
    {
        return HasLevel(geometryLevels, level);
    }

    internal bool TryMarkGeometry(int level)
    {
        return TryMark(ref geometryLevels, level);
    }

    internal bool HasPlayerHit(int level)
    {
        return HasLevel(playerHitLevels, level);
    }

    internal bool TryMarkPlayerHit(int level)
    {
        return TryMark(ref playerHitLevels, level);
    }

    private static bool TryMark(ref int levels, int level)
    {
        if (!TryGetLevelBit(level, out int levelBit))
        {
            return false;
        }

        if ((levels & levelBit) != 0)
        {
            return false;
        }

        levels |= levelBit;
        return true;
    }

    private static bool HasLevel(int levels, int level)
    {
        return TryGetLevelBit(level, out int levelBit) && (levels & levelBit) != 0;
    }

    private static bool TryGetLevelBit(int level, out int levelBit)
    {
        if (level < 0 || level >= 31)
        {
            levelBit = 0;
            return false;
        }

        levelBit = 1 << level;
        return true;
    }
}

internal static class BoarTierHitObservationRules
{
    internal static bool ShouldObserve(
        bool profileApplied,
        bool localPlayerAvailable,
        bool attackerIsLocalPlayer,
        bool ranged,
        bool colliderAvailable)
    {
        return profileApplied &&
            localPlayerAvailable &&
            attackerIsLocalPlayer &&
            !ranged &&
            colliderAvailable;
    }
}
