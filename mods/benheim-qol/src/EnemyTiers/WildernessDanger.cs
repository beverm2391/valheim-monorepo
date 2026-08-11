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
