using System;

namespace BenheimQoL.Grappling;

internal static class GrapplingRules
{
    internal const float MinimumMovement = 3f;
    internal const float BaseExperience = 0.25f;
    internal const float ExperiencePerMeter = 0.04f;
    internal const float UphillExperiencePerMeter = 0.02f;
    internal const float MaximumExperience = 3f;

    internal static float ReachMultiplier(float level) => 1f + Progress(level);
    internal static float ReloadMultiplier(float level) => 1f - 0.5f * Progress(level);

    internal static float Experience(float attachmentDistance, float movementSinceAttachment,
        float displacement, float heightGained)
    {
        // Require traversal after attachment as well as net displacement since
        // firing. Running during projectile flight or firing beside yourself
        // must not turn a stationary tether into an XP award.
        if (!Finite(attachmentDistance) || !Finite(movementSinceAttachment)
            || !Finite(displacement) || !Finite(heightGained)
            || attachmentDistance < MinimumMovement
            || movementSinceAttachment < MinimumMovement || displacement < MinimumMovement)
        {
            return 0f;
        }

        return Math.Min(MaximumExperience, BaseExperience + displacement * ExperiencePerMeter
            + Math.Max(0f, heightGained) * UphillExperiencePerMeter);
    }

    private static float Progress(float level) =>
        Finite(level) ? Math.Max(0f, Math.Min(1f, level / 100f)) : 0f;

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
