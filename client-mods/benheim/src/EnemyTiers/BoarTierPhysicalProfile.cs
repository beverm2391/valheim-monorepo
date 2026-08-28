namespace BenheimQoL.EnemyTiers;

internal readonly struct BoarTierPhysicalProfile
{
    internal const float NativeColliderCenterY = 0.7f;
    internal const float NativeColliderRadius = 0.5f;
    internal const float NativeColliderHeight = 1.4f;

    private BoarTierPhysicalProfile(
        float scale,
        float incomingPushMultiplier,
        float outgoingPushMultiplier,
        float detectionMultiplier,
        float alertRangeMultiplier,
        float runSpeedMultiplier,
        float runTurnSpeedMultiplier,
        float pursuitDurationMultiplier)
    {
        VisualScale = scale;
        ColliderCenterY = NativeColliderCenterY * scale;
        ColliderRadius = NativeColliderRadius * scale;
        ColliderHeight = NativeColliderHeight * scale;
        IncomingPushMultiplier = incomingPushMultiplier;
        OutgoingPushMultiplier = outgoingPushMultiplier;
        DetectionMultiplier = detectionMultiplier;
        AlertRangeMultiplier = alertRangeMultiplier;
        RunSpeedMultiplier = runSpeedMultiplier;
        RunTurnSpeedMultiplier = runTurnSpeedMultiplier;
        PursuitDurationMultiplier = pursuitDurationMultiplier;
    }

    internal float VisualScale { get; }
    internal float ColliderCenterY { get; }
    internal float ColliderRadius { get; }
    internal float ColliderHeight { get; }
    internal float IncomingPushMultiplier { get; }
    internal float OutgoingPushMultiplier { get; }
    internal float DetectionMultiplier { get; }
    internal float AlertRangeMultiplier { get; }
    internal float RunSpeedMultiplier { get; }
    internal float RunTurnSpeedMultiplier { get; }
    internal float PursuitDurationMultiplier { get; }

    internal float PursuitTimerCompensation(float dt)
    {
        return PursuitDurationMultiplier > 1f
            ? dt * (1f - (1f / PursuitDurationMultiplier))
            : 0f;
    }

    internal float CompensatePursuitTimer(float elapsed, float dt)
    {
        float compensated = elapsed - PursuitTimerCompensation(dt);
        return compensated > 0f ? compensated : 0f;
    }

    internal static bool TryForLevel(int level, out BoarTierPhysicalProfile profile)
    {
        switch (level)
        {
            case 2:
                profile = new BoarTierPhysicalProfile(
                    scale: 1.4f,
                    incomingPushMultiplier: 0.75f,
                    outgoingPushMultiplier: 1.25f,
                    detectionMultiplier: 1.2f,
                    alertRangeMultiplier: 1.5f,
                    runSpeedMultiplier: 1.08f,
                    runTurnSpeedMultiplier: 0.85f,
                    pursuitDurationMultiplier: 1.25f);
                return true;
            case 3:
                profile = new BoarTierPhysicalProfile(
                    scale: 1.7f,
                    incomingPushMultiplier: 0.55f,
                    outgoingPushMultiplier: 1.5f,
                    detectionMultiplier: 1.4f,
                    alertRangeMultiplier: 2f,
                    runSpeedMultiplier: 1.15f,
                    runTurnSpeedMultiplier: 0.7f,
                    pursuitDurationMultiplier: 1.5f);
                return true;
            default:
                profile = default;
                return false;
        }
    }
}

internal sealed class BoarTierApplicationState
{
    private bool baselineCaptured;

    internal bool ProfileApplied { get; private set; }
    internal float NativeRunSpeed { get; private set; }
    internal float NativeRunTurnSpeed { get; private set; }
    internal float NativeViewRange { get; private set; }
    internal float NativeHearRange { get; private set; }
    internal float NativeAlertRange { get; private set; }
    internal bool NativeFleeIfNotAlerted { get; private set; }

    internal void CaptureBaseline(
        float runSpeed,
        float runTurnSpeed,
        float viewRange,
        float hearRange,
        float alertRange,
        bool fleeIfNotAlerted)
    {
        if (baselineCaptured)
        {
            return;
        }

        NativeRunSpeed = runSpeed;
        NativeRunTurnSpeed = runTurnSpeed;
        NativeViewRange = viewRange;
        NativeHearRange = hearRange;
        NativeAlertRange = alertRange;
        NativeFleeIfNotAlerted = fleeIfNotAlerted;
        baselineCaptured = true;
    }

    internal void MarkApplied()
    {
        ProfileApplied = true;
    }

    internal void MarkRestored()
    {
        ProfileApplied = false;
    }
}
