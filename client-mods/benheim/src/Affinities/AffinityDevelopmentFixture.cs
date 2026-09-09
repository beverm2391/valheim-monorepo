namespace BenheimQoL.Affinities;

public sealed class AffinityDevelopmentFixtureResult
{
    internal AffinityDevelopmentFixtureResult(bool applied, string reason)
    {
        Applied = applied;
        Reason = reason;
    }

    public bool Applied { get; }
    public string Reason { get; }
}

public static class AffinityDevelopmentFixture
{
    // This is the one public mutation seam for Lab recipes. Keep item identity,
    // eligibility, state writes, inventory notification, and diagnostics owned
    // by the same application path used by Benheim's developer command.
    public static AffinityDevelopmentFixtureResult ApplyToEquippedWeapon(string affinity)
    {
        if (!TryParse(affinity, out AffinityLoadResult selected))
        {
            return new AffinityDevelopmentFixtureResult(false, "unsupported_affinity");
        }

        return ApplyToEquippedWeapon(selected, "lab_fixture");
    }

    internal static AffinityDevelopmentFixtureResult ApplyToEquippedWeapon(
        AffinityLoadResult selected,
        string source)
    {
        Player? player = Player.m_localPlayer;
        AffinityApplicationResult result = AffinityApplication.Apply(
            player,
            player?.GetCurrentWeapon(),
            selected,
            requireForge: false,
            consumeResources: false,
            source,
            developerBypass: true);
        return new AffinityDevelopmentFixtureResult(result.Applied, result.Reason);
    }

    private static bool TryParse(string affinity, out AffinityLoadResult selected)
    {
        selected = affinity?.ToLowerInvariant() switch
        {
            "lunge" => AffinityLoadResult.Lunge,
            "snipe" => AffinityLoadResult.Snipe,
            "test" => AffinityLoadResult.Test,
            _ => AffinityLoadResult.None,
        };
        return selected != AffinityLoadResult.None;
    }
}
