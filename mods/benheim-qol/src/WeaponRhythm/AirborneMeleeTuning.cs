namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMeleeTuning
{
    // First-playtest value. A small negative threshold rejects apex jitter
    // without requiring a large fall before the authored hit can qualify.
    internal const float DescentThreshold = -0.5f;

    // The Player prefab's native sprint speed is 7 m/s. A normal 4 m/s
    // jog-jump gains at most 2.8 m/s from max Jump skill, so this physical
    // toward-target threshold separates ordinary jump momentum from the
    // native sprint band without depending on the sprint input state.
    internal const float ApproachSpeedThreshold = 7f;

    internal const float DamageMultiplier = 1.15f;
    internal const float StaggerMultiplier = 2f;
}
