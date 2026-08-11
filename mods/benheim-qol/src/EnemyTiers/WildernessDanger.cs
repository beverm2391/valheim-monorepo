namespace BenheimQoL.EnemyTiers;

internal enum WildernessDanger
{
    Familiar,
    Sketchy,
    Dangerous,
    Deadly,
}

internal static class WildernessDangerScale
{
    internal const float SketchyThreshold = 12f;
    internal const float DangerousThreshold = 18f;
    internal const float DeadlyThreshold = 24f;

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

        return WildernessDanger.Familiar;
    }

    internal static string Label(WildernessDanger danger)
    {
        return danger.ToString();
    }

    internal static bool IsVisible(bool locallyExplored, bool sharedExplored, bool showSharedMapData)
    {
        return locallyExplored || (showSharedMapData && sharedExplored);
    }
}
