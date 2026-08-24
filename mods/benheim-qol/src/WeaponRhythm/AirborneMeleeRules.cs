namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMeleeRules
{
    internal static float ProjectPlanarVelocityToward(
        float velocityX,
        float velocityZ,
        float directionX,
        float directionZ)
    {
        float lengthSquared = (directionX * directionX) + (directionZ * directionZ);
        if (lengthSquared <= 0.0001f)
        {
            return 0f;
        }

        float inverseLength = 1f / System.MathF.Sqrt(lengthSquared);
        return ((velocityX * directionX) + (velocityZ * directionZ)) * inverseLength;
    }

    internal static PerfectImpactResolution ResolveContact(
        bool attackerIsGrounded,
        float verticalSpeed,
        float descentThreshold,
        float towardTargetSpeed,
        float approachSpeedThreshold)
    {
        if (attackerIsGrounded)
        {
            return PerfectImpactResolution.Grounded;
        }

        if (verticalSpeed > descentThreshold)
        {
            return PerfectImpactResolution.RisingOrApex;
        }

        return towardTargetSpeed >= approachSpeedThreshold
            ? PerfectImpactResolution.Applied
            : PerfectImpactResolution.InsufficientApproach;
    }
}
