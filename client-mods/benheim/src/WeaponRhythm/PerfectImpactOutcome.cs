namespace BenheimQoL.WeaponRhythm;

internal enum PerfectImpactResolution
{
    Applied,
    Grounded,
    RisingOrApex,
    InsufficientApproach
}

/// <summary>
/// One immutable decision from the first authored Character contact of a
/// supported native attack. Later contacts may reuse the attack's decision,
/// but they cannot create or reverse its player-facing outcome.
/// </summary>
internal sealed class PerfectImpactOutcome
{
    internal PerfectImpactOutcome(
        string operationId,
        PerfectImpactResolution resolution,
        string weapon,
        string attackControl,
        string attackAnimation,
        string attackType,
        string skill,
        string target,
        bool attackerGrounded,
        float verticalSpeed,
        float descentThreshold,
        float towardTargetSpeed,
        float approachThreshold,
        float damageMultiplier,
        float staggerMultiplier)
    {
        OperationId = operationId;
        Resolution = resolution;
        Weapon = weapon;
        AttackControl = attackControl;
        AttackAnimation = attackAnimation;
        AttackType = attackType;
        Skill = skill;
        Target = target;
        AttackerGrounded = attackerGrounded;
        VerticalSpeed = verticalSpeed;
        DescentThreshold = descentThreshold;
        TowardTargetSpeed = towardTargetSpeed;
        ApproachThreshold = approachThreshold;
        DamageMultiplier = damageMultiplier;
        StaggerMultiplier = staggerMultiplier;
    }

    internal string OperationId { get; }
    internal PerfectImpactResolution Resolution { get; }
    internal string Weapon { get; }
    internal string AttackControl { get; }
    internal string AttackAnimation { get; }
    internal string AttackType { get; }
    internal string Skill { get; }
    internal string Target { get; }
    internal bool AttackerGrounded { get; }
    internal float VerticalSpeed { get; }
    internal float DescentThreshold { get; }
    internal float TowardTargetSpeed { get; }
    internal float ApproachThreshold { get; }
    internal float DamageMultiplier { get; }
    internal float StaggerMultiplier { get; }
    internal bool Qualified => Resolution == PerfectImpactResolution.Applied;
    internal bool FeedbackRequested => Qualified;
    internal string FeedbackSeam => Qualified ? "native_world_text" : "not_requested";
}

internal sealed class AirborneMeleeSwingState
{
    internal AirborneMeleeSwingState(
        string operationId,
        string weapon,
        string attackControl,
        string attackAnimation,
        string attackType)
    {
        OperationId = operationId;
        Weapon = weapon;
        AttackControl = attackControl;
        AttackAnimation = attackAnimation;
        AttackType = attackType;
    }

    internal string OperationId { get; }
    internal string Weapon { get; }
    internal string AttackControl { get; }
    internal string AttackAnimation { get; }
    internal string AttackType { get; }
    internal bool Resolved { get; private set; }
    internal bool Qualified { get; private set; }

    internal bool TryResolve(PerfectImpactResolution resolution)
    {
        if (Resolved)
        {
            return false;
        }

        Resolved = true;
        Qualified = resolution == PerfectImpactResolution.Applied;
        return true;
    }
}
