using System;

namespace BenheimQoL.CombatFeedback;

internal enum CombatFeedbackTrigger
{
    Headshot,
    Cleave,
    MiningAoe
}

/// <summary>
/// Owns the deliberately experimental feel values for Combat Feedback. Keep
/// these values together so a gameplay pass can tune the whole interaction
/// without searching through patches or feature modules.
/// </summary>
internal static class CombatFeedbackTuning
{
    internal const float BowFocusMaxReductionDegrees = 5f;
    internal const float BowFocusNarrowSmoothSeconds = 0.14f;
    internal const float BowFocusRestoreSmoothSeconds = 0.10f;

    internal const float HeadshotShakeStrength = 0.45f;
    internal const float CleaveShakeStrength = 0.32f;
    internal const float MiningAoeShakeStrength = 0.38f;
    internal const float ShakeStrengthCap = 0.45f;
    internal const float ShakeCoalesceSeconds = 0.12f;
    internal const float ShakeRangeMeters = 1000f;

    internal static float FocusReduction(float drawPercentage)
    {
        float draw = Clamp01(drawPercentage);
        float eased = draw * draw * (3f - (2f * draw));
        return BowFocusMaxReductionDegrees * eased;
    }

    internal static float ShakeStrength(CombatFeedbackTrigger trigger)
    {
        float requested = trigger switch
        {
            CombatFeedbackTrigger.Headshot => HeadshotShakeStrength,
            CombatFeedbackTrigger.Cleave => CleaveShakeStrength,
            CombatFeedbackTrigger.MiningAoe => MiningAoeShakeStrength,
            _ => 0f
        };

        return Math.Min(requested, ShakeStrengthCap);
    }

    internal static bool ShouldApplyShake(float secondsSinceLastShake, float activeStrength, float requestedStrength)
    {
        return secondsSinceLastShake >= ShakeCoalesceSeconds || requestedStrength > activeStrength;
    }

    private static float Clamp01(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
        {
            return 0f;
        }

        return value >= 1f ? 1f : value;
    }
}
