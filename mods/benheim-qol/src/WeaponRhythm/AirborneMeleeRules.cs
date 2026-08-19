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

internal sealed class AirborneMeleeSwingState : AirborneMeleeStartIdentity
{
    internal AirborneMeleeSwingState(
        AirborneMeleeStartIdentity attempt,
        bool armed,
        bool startGateObserved)
        : base(
            attempt.OperationId,
            attempt.Weapon,
            attempt.AttackControl,
            attempt.AttackAnimation,
            attempt.AttackType,
            attempt.StartVerticalSpeed,
            attempt.StartForwardSpeed,
            attempt.StartedGrounded)
    {
        Armed = armed;
        StartGateObserved = startGateObserved;
    }

    internal bool Armed { get; }
    internal bool StartGateObserved { get; private set; }
    internal bool Resolved { get; private set; }
    internal bool Qualified { get; private set; }

    internal bool MarkStartGateObserved()
    {
        if (StartGateObserved)
        {
            return false;
        }

        StartGateObserved = true;
        Resolved = true;
        return true;
    }

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

internal class AirborneMeleeStartIdentity
{
    internal AirborneMeleeStartIdentity(
        string operationId,
        string weapon,
        string attackControl,
        string attackAnimation,
        string attackType,
        float startVerticalSpeed,
        float startForwardSpeed,
        bool startedGrounded)
    {
        OperationId = operationId;
        Weapon = weapon;
        AttackControl = attackControl;
        AttackAnimation = attackAnimation;
        AttackType = attackType;
        StartVerticalSpeed = startVerticalSpeed;
        StartForwardSpeed = startForwardSpeed;
        StartedGrounded = startedGrounded;
    }

    internal string OperationId { get; }
    internal string Weapon { get; }
    internal string AttackControl { get; }
    internal string AttackAnimation { get; }
    internal string AttackType { get; }
    internal float StartVerticalSpeed { get; }
    internal float StartForwardSpeed { get; }
    internal bool StartedGrounded { get; }
}

internal sealed class AirborneMeleeStartAttempt : AirborneMeleeStartIdentity
{
    internal AirborneMeleeStartAttempt(
        string operationId,
        string weapon,
        string attackControl,
        string attackAnimation,
        string attackType,
        float startVerticalSpeed,
        float startForwardSpeed,
        bool startedGrounded,
        bool freshInput)
        : base(
            operationId,
            weapon,
            attackControl,
            attackAnimation,
            attackType,
            startVerticalSpeed,
            startForwardSpeed,
            startedGrounded)
    {
        FreshInput = freshInput;
    }

    internal bool FreshInput { get; }
}
