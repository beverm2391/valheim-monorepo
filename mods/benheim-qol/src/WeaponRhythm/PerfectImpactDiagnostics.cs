using BenheimQoL.Infrastructure;

namespace BenheimQoL.WeaponRhythm;

internal static class PerfectImpactDiagnostics
{
    internal static void Emit(PerfectImpactOutcome outcome)
    {
        Diagnostics.Emit(CreateEvent(outcome));
    }

    internal static DiagnosticEvent CreateEvent(PerfectImpactOutcome outcome)
    {
        return DiagnosticEvent.Create("WeaponRhythm", "perfect_impact_outcome")
            .String("operation_id", outcome.OperationId)
            .String("operation_phase", "terminal")
            .Boolean("qualified", outcome.Qualified)
            .String("reason", ReasonName(outcome.Resolution))
            .String("weapon", outcome.Weapon)
            .String("attack_control", outcome.AttackControl)
            .String("attack_animation", outcome.AttackAnimation)
            .String("attack_type", outcome.AttackType)
            .String("skill", outcome.Skill)
            .String("target", outcome.Target)
            .Boolean("attacker_grounded", outcome.AttackerGrounded)
            .Number("vertical_speed", outcome.VerticalSpeed)
            .Number("descent_threshold", outcome.DescentThreshold)
            .Number("toward_target_speed", outcome.TowardTargetSpeed)
            .Number("approach_threshold", outcome.ApproachThreshold)
            .Number("damage_multiplier", outcome.DamageMultiplier)
            .Number("stagger_multiplier", outcome.StaggerMultiplier)
            .String("feedback", outcome.Feedback);
    }

    private static string ReasonName(PerfectImpactResolution resolution)
    {
        return resolution switch
        {
            PerfectImpactResolution.Applied => "applied",
            PerfectImpactResolution.Grounded => "grounded",
            PerfectImpactResolution.RisingOrApex => "rising_or_apex",
            _ => "insufficient_approach"
        };
    }
}
