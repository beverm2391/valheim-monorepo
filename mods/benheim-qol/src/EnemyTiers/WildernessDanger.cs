namespace BenheimQoL.EnemyTiers;

internal enum WildernessDanger
{
    Safe,
    Sketchy,
    Dangerous,
    Deadly,
}

internal static class WildernessDangerScale
{
    internal const float SketchyThreshold = 17.5f;
    internal const float DangerousThreshold = 25f;
    internal const float DeadlyThreshold = 32.5f;

    internal static WildernessDanger Classify(float perStepChance)
    {
        if (perStepChance >= DeadlyThreshold)
        {
            return WildernessDanger.Deadly;
        }

        if (perStepChance >= DangerousThreshold)
        {
            return WildernessDanger.Dangerous;
        }

        if (perStepChance >= SketchyThreshold)
        {
            return WildernessDanger.Sketchy;
        }

        return WildernessDanger.Safe;
    }

    internal static string StyledArrivalLabel(WildernessDanger danger)
    {
        return danger switch
        {
            WildernessDanger.Safe => "<color=#6F9F6A>SAFE</color>",
            WildernessDanger.Sketchy => "<color=#B59A45>SKETCHY</color>",
            WildernessDanger.Dangerous => "<color=#C8753B><b>DANGEROUS</b></color>",
            WildernessDanger.Deadly => "<color=#C94F55><b>DEADLY</b></color>",
            _ => throw new System.ArgumentOutOfRangeException(nameof(danger), danger, null),
        };
    }

    internal static string MapLabel(WildernessDanger danger)
    {
        return danger switch
        {
            WildernessDanger.Safe => "SAFE",
            WildernessDanger.Sketchy => "SKETCHY",
            WildernessDanger.Dangerous => "DANGEROUS",
            WildernessDanger.Deadly => "DEADLY",
            _ => throw new System.ArgumentOutOfRangeException(nameof(danger), danger, null),
        };
    }

    internal static string MinimapLabel(WildernessDanger danger)
    {
        return danger switch
        {
            WildernessDanger.Safe => "Safe",
            WildernessDanger.Sketchy => "Sketchy",
            WildernessDanger.Dangerous => "Dangerous",
            WildernessDanger.Deadly => "Deadly",
            _ => throw new System.ArgumentOutOfRangeException(nameof(danger), danger, null),
        };
    }

    internal static bool IsVisible(bool locallyExplored, bool sharedExplored, bool showSharedMapData)
    {
        return locallyExplored || (showSharedMapData && sharedExplored);
    }
}
