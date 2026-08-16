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

    internal static bool CanArm(
        bool attackerIsLocalPlayer,
        bool meleeAttack,
        bool attackerIsGrounded,
        float forwardSpeed,
        float forwardSpeedThreshold)
    {
        return attackerIsLocalPlayer
            && meleeAttack
            && !attackerIsGrounded
            && forwardSpeed >= forwardSpeedThreshold;
    }

    internal static bool CanConsume(
        bool armed,
        bool targetIsCharacter,
        bool attackerIsLocalPlayer,
        bool attackerIsGrounded,
        float verticalSpeed,
        float descentThreshold)
    {
        return armed
            && targetIsCharacter
            && attackerIsLocalPlayer
            && !attackerIsGrounded
            && verticalSpeed <= descentThreshold;
    }
}

internal sealed class AirborneMeleeSwingState
{
    internal AirborneMeleeSwingState(
        string operationId,
        float startVerticalSpeed,
        float startForwardSpeed)
    {
        OperationId = operationId;
        StartVerticalSpeed = startVerticalSpeed;
        StartForwardSpeed = startForwardSpeed;
    }

    internal string OperationId { get; }
    internal float StartVerticalSpeed { get; }
    internal float StartForwardSpeed { get; }
    internal bool Resolved { get; private set; }
    internal bool Qualified { get; private set; }

    internal bool Resolve(bool qualified)
    {
        if (Resolved)
        {
            return false;
        }

        Resolved = true;
        Qualified = qualified;
        return true;
    }
}
