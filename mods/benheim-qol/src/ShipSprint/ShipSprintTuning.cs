namespace BenheimQoL.ShipSprint;

internal static class ShipSprintTuning
{
    // This multiplies the native force applied during this physics step. Drag,
    // wind effectiveness, and every other ship force remain native, so this is
    // deliberately not a terminal-velocity promise.
    internal const float ThrustMultiplier = 3f;
    internal const float RequestHeartbeatSeconds = 0.25f;
}
