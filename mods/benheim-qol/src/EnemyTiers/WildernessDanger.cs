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

    internal static string StyledLabel(WildernessDanger danger)
    {
        return danger switch
        {
            WildernessDanger.Safe => "<color=#A8D8A0>SAFE</color>",
            WildernessDanger.Sketchy => "<color=#F0D36B>SKETCHY</color>",
            WildernessDanger.Dangerous => "<color=#FF9B4A><b>DANGEROUS</b></color>",
            WildernessDanger.Deadly => "<color=#FF5C5C><b>DEADLY</b></color>",
            _ => throw new System.ArgumentOutOfRangeException(nameof(danger), danger, null),
        };
    }

    internal static bool IsVisible(bool locallyExplored, bool sharedExplored, bool showSharedMapData)
    {
        return locallyExplored || (showSharedMapData && sharedExplored);
    }
}
