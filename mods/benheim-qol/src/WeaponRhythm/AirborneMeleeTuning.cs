namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMeleeTuning
{
    // First-playtest value. A small negative threshold rejects apex jitter
    // without requiring a large fall before the authored hit can qualify.
    internal const float DescentThreshold = -0.5f;

    // The Player prefab's native sprint speed is 7 m/s. Measure physical
    // velocity toward the authored contact instead of reading sprint input.
    internal const float ApproachSpeedThreshold = 7f;

    internal const float DamageMultiplier = 1.15f;
    internal const float StaggerMultiplier = 3f;
}
