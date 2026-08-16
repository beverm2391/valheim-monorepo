namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMeleeTuning
{
    // First-playtest value. A small negative threshold rejects apex jitter
    // without requiring a large fall before the authored hit can qualify.
    internal const float DescentThreshold = -0.5f;

    // The Player prefab's native sprint speed is 7 m/s. Capture that physical
    // forward momentum when the Attack clone starts, before native attack
    // movement and contact can slow it. This does not depend on sprint input.
    internal const float ForwardSpeedThreshold = 7f;

    internal const float DamageMultiplier = 1.15f;
    internal const float StaggerMultiplier = 3f;
}
