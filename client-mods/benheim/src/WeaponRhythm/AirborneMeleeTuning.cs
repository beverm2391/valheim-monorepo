namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMeleeTuning
{
    // First-playtest value. A small negative threshold rejects apex jitter
    // without requiring a large fall before the authored hit can qualify.
    internal const float DescentThreshold = -0.5f;

    // The 7 m/s sprint-band value did not survive through contact in the first
    // live test. Measure physical velocity toward the authored contact and use
    // 5.5 m/s as the next playtest threshold.
    internal const float ApproachSpeedThreshold = 5.5f;

    internal const float DamageMultiplier = 1.15f;
    internal const float StaggerMultiplier = 3f;
}
