using System.Runtime.CompilerServices;
using BenheimQoL.CombatFeedback;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMelee
{
    private const string SuccessMessage = "PERFECT IMPACT";
    private static readonly ConditionalWeakTable<Attack, AirborneMeleeSwingState> SwingStates = new();

    internal static void ObserveAttackStart(Attack attack, Humanoid character, bool started)
    {
        Player? localPlayer = Player.m_localPlayer;
        if (!started
            || localPlayer == null
            || character != localPlayer
            || !IsMeleeAttack(attack.m_attackType)
            || localPlayer.IsOnGround())
        {
            return;
        }

        Vector3 velocity = localPlayer.GetVelocity();
        Vector3 forward = localPlayer.transform.forward;
        float forwardSpeed = AirborneMeleeRules.ProjectPlanarVelocityToward(
            velocity.x,
            velocity.z,
            forward.x,
            forward.z);
        bool armed = AirborneMeleeRules.CanArm(
            attackerIsLocalPlayer: true,
            meleeAttack: true,
            attackerIsGrounded: false,
            forwardSpeed: forwardSpeed,
            forwardSpeedThreshold: AirborneMeleeTuning.ForwardSpeedThreshold);
        string reason = armed ? "forward_sprint_momentum" : "insufficient_forward_momentum";
        string operationId = Diagnostics.NewOperationId();
        if (armed)
        {
            AirborneMeleeSwingState state = new AirborneMeleeSwingState(
                operationId,
                velocity.y,
                forwardSpeed);
            SwingStates.Remove(attack);
            SwingStates.Add(attack, state);
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create(
                    "WeaponRhythm",
                    armed ? "airborne_melee_armed" : "airborne_melee_arm_rejected")
                .String("operation_id", operationId)
                .String("operation_phase", armed ? "start" : "terminal")
                .String("reason", reason)
                .String("attack_type", attack.m_attackType.ToString())
                .Number("start_vertical_speed", velocity.y)
                .Number("start_forward_speed", forwardSpeed)
                .Number("forward_speed_threshold", AirborneMeleeTuning.ForwardSpeedThreshold)
                .Number("damage_multiplier", AirborneMeleeTuning.DamageMultiplier)
                .Number("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier)
                .String("feedback", "not_requested"));
    }

    internal static void DamageMeleeTarget(IDestructible target, HitData hit, Attack attack)
    {
        Character? targetCharacter = target as Character;
        Player? localPlayer = Player.m_localPlayer;
        Character? attacker = hit.GetAttacker();
        bool localAttack = localPlayer != null && attacker == localPlayer;

        if (localAttack
            && targetCharacter != null
            && SwingStates.TryGetValue(attack, out AirborneMeleeSwingState? state))
        {
            bool grounded = localPlayer!.IsOnGround();
            Vector3 velocity = localPlayer.GetVelocity();
            float verticalSpeed = velocity.y;
            bool qualifiesAtHit = AirborneMeleeRules.CanConsume(
                armed: true,
                targetIsCharacter: true,
                attackerIsLocalPlayer: true,
                attackerIsGrounded: grounded,
                verticalSpeed: verticalSpeed,
                descentThreshold: AirborneMeleeTuning.DescentThreshold);
            bool firstResolution = state.Resolve(qualifiesAtHit);

            // A native area attack resolves its targets synchronously. Preserve
            // the existing per-target modifiers while the same resolved swing
            // remains airborne and descending, but present and log it once.
            if (state.Qualified && qualifiesAtHit)
            {
                hit.m_damage.Modify(AirborneMeleeTuning.DamageMultiplier);
                hit.m_staggerMultiplier *= AirborneMeleeTuning.StaggerMultiplier;
            }

            if (firstResolution && state.Qualified)
            {
                string feedbackResult = ShowPerfectImpactFeedback();
                Diagnostics.Emit(
                    DiagnosticEvent.Create("WeaponRhythm", "airborne_melee_applied")
                        .String("operation_id", state.OperationId)
                        .String("operation_phase", "terminal")
                        .String("skill", hit.m_skill.ToString())
                        .String("target", TargetName(targetCharacter))
                        .Number("start_vertical_speed", state.StartVerticalSpeed)
                        .Number("start_forward_speed", state.StartForwardSpeed)
                        .Number("forward_speed_threshold", AirborneMeleeTuning.ForwardSpeedThreshold)
                        .Number("vertical_speed", verticalSpeed)
                        .Number("descent_threshold", AirborneMeleeTuning.DescentThreshold)
                        .Number("damage_multiplier", AirborneMeleeTuning.DamageMultiplier)
                        .Number("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier)
                        .String("feedback", feedbackResult));
            }
            else if (firstResolution)
            {
                string reason = grounded ? "grounded_at_hit" : "rising_or_apex_at_hit";
                Diagnostics.Emit(
                    DiagnosticEvent.Create("WeaponRhythm", "airborne_melee_skipped")
                        .String("operation_id", state.OperationId)
                        .String("operation_phase", "terminal")
                        .String("reason", reason)
                        .String("skill", hit.m_skill.ToString())
                        .String("target", TargetName(targetCharacter))
                        .Number("start_vertical_speed", state.StartVerticalSpeed)
                        .Number("start_forward_speed", state.StartForwardSpeed)
                        .Number("forward_speed_threshold", AirborneMeleeTuning.ForwardSpeedThreshold)
                        .Number("vertical_speed", verticalSpeed)
                        .Number("descent_threshold", AirborneMeleeTuning.DescentThreshold)
                        .Number("damage_multiplier", AirborneMeleeTuning.DamageMultiplier)
                        .Number("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier)
                        .String("feedback", "not_requested"));
            }
        }

        target.Damage(hit);
    }

    internal static void ObserveAttackStop(Attack attack)
    {
        if (!SwingStates.TryGetValue(attack, out AirborneMeleeSwingState? state))
        {
            return;
        }

        if (state.Resolve(qualified: false))
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("WeaponRhythm", "airborne_melee_skipped")
                    .String("operation_id", state.OperationId)
                    .String("operation_phase", "terminal")
                    .String("reason", "no_character_contact")
                    .Number("start_vertical_speed", state.StartVerticalSpeed)
                    .Number("start_forward_speed", state.StartForwardSpeed)
                    .Number("forward_speed_threshold", AirborneMeleeTuning.ForwardSpeedThreshold)
                    .Number("descent_threshold", AirborneMeleeTuning.DescentThreshold)
                    .Number("damage_multiplier", AirborneMeleeTuning.DamageMultiplier)
                    .Number("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier)
                    .String("feedback", "not_requested"));
        }

        SwingStates.Remove(attack);
    }

    private static string ShowPerfectImpactFeedback()
    {
        TopLeftFeedbackResult feedbackResult = TopLeftFeedbackHud.ShowTransient(SuccessMessage);
        CombatFeedbackController.RequestShake(CombatFeedbackTrigger.PerfectImpact);
        return feedbackResult switch
        {
            TopLeftFeedbackResult.Placed => "placed",
            TopLeftFeedbackResult.CreatedNotPlaced => "created_not_placed",
            _ => "unavailable"
        };
    }

    private static bool IsMeleeAttack(Attack.AttackType attackType)
    {
        return attackType == Attack.AttackType.Horizontal
            || attackType == Attack.AttackType.Vertical
            || attackType == Attack.AttackType.Area;
    }

    private static string TargetName(Character target)
    {
        return Diagnostics.Flatten(target.gameObject.name);
    }
}
