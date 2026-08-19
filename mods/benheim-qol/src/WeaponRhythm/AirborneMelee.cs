using System.Runtime.CompilerServices;
using BenheimQoL.CombatFeedback;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMelee
{
    private const string SuccessMessage = "PERFECT IMPACT";
    private static readonly ConditionalWeakTable<Attack, AirborneMeleeSwingState> SwingStates = new();

    internal static AirborneMeleeStartAttempt? BeginAttackAttempt(
        Humanoid character,
        bool secondaryAttack,
        bool freshInput)
    {
        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null || character != localPlayer)
        {
            return null;
        }

        ItemDrop.ItemData? weapon = localPlayer.GetCurrentWeapon();
        if (weapon == null)
        {
            return null;
        }
        Attack attack = secondaryAttack
            ? weapon.m_shared.m_secondaryAttack
            : weapon.m_shared.m_attack;
        if (!IsMeleeAttack(attack.m_attackType))
        {
            return null;
        }

        Vector3 velocity = localPlayer.GetVelocity();
        Vector3 forward = localPlayer.transform.forward;
        float forwardSpeed = AirborneMeleeRules.ProjectPlanarVelocityToward(
            velocity.x,
            velocity.z,
            forward.x,
            forward.z);
        return new AirborneMeleeStartAttempt(
            Diagnostics.NewOperationId(),
            weapon.m_shared.m_name,
            secondaryAttack ? "secondary" : "primary",
            attack.m_attackAnimation,
            attack.m_attackType.ToString(),
            velocity.y,
            forwardSpeed,
            localPlayer.IsOnGround(),
            freshInput);
    }

    internal static void CompleteAttackAttempt(
        AirborneMeleeStartAttempt? attempt,
        Attack? startedAttack,
        bool started)
    {
        if (attempt == null)
        {
            return;
        }

        if (!started)
        {
            if (!attempt.StartedGrounded && attempt.FreshInput)
            {
                EmitStartDecision(
                    attempt,
                    "airborne_melee_arm_rejected",
                    "native_start_rejected",
                    nativeAttackStarted: false);
            }
            return;
        }

        if (startedAttack == null)
        {
            EmitStartDecision(
                attempt,
                "airborne_melee_arm_rejected",
                "started_clone_unavailable",
                nativeAttackStarted: true);
            return;
        }

        if (attempt.StartedGrounded)
        {
            ReplaceSwingState(
                startedAttack,
                new AirborneMeleeSwingState(attempt, armed: false, startGateObserved: false));
            return;
        }

        bool armed = AirborneMeleeRules.CanArm(
            attackerIsLocalPlayer: true,
            meleeAttack: true,
            attackerIsGrounded: false,
            forwardSpeed: attempt.StartForwardSpeed,
            forwardSpeedThreshold: AirborneMeleeTuning.ForwardSpeedThreshold);
        string reason = armed ? "forward_sprint_momentum" : "insufficient_forward_momentum";
        if (armed)
        {
            ReplaceSwingState(
                startedAttack,
                new AirborneMeleeSwingState(attempt, armed: true, startGateObserved: true));
        }

        EmitStartDecision(
            attempt,
            armed ? "airborne_melee_armed" : "airborne_melee_arm_rejected",
            reason,
            nativeAttackStarted: true);
    }

    internal static void ObserveAttackProgress(Attack attack)
    {
        if (!SwingStates.TryGetValue(attack, out AirborneMeleeSwingState? state)
            || state.Armed
            || state.StartGateObserved)
        {
            return;
        }

        ObserveGroundedStartBecameAirborne(state);
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
            if (!state.Armed)
            {
                ObserveGroundedStartBecameAirborne(state);
                target.Damage(hit);
                return;
            }

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
                        .String("weapon", state.Weapon)
                        .String("attack_control", state.AttackControl)
                        .String("attack_animation", state.AttackAnimation)
                        .String("attack_type", state.AttackType)
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
                        .String("weapon", state.Weapon)
                        .String("attack_control", state.AttackControl)
                        .String("attack_animation", state.AttackAnimation)
                        .String("attack_type", state.AttackType)
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

        if (!state.Armed)
        {
            ObserveGroundedStartBecameAirborne(state);
        }
        else if (state.Resolve(qualified: false))
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("WeaponRhythm", "airborne_melee_skipped")
                    .String("operation_id", state.OperationId)
                    .String("operation_phase", "terminal")
                    .String("reason", "no_character_contact")
                    .String("weapon", state.Weapon)
                    .String("attack_control", state.AttackControl)
                    .String("attack_animation", state.AttackAnimation)
                    .String("attack_type", state.AttackType)
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

    private static void ObserveGroundedStartBecameAirborne(AirborneMeleeSwingState state)
    {
        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null
            || localPlayer.IsOnGround()
            || !state.MarkStartGateObserved())
        {
            return;
        }

        EmitStartDecision(
            state,
            "airborne_melee_arm_rejected",
            "grounded_at_start",
            nativeAttackStarted: true,
            currentVerticalSpeed: localPlayer.GetVelocity().y);
    }

    private static void ReplaceSwingState(Attack attack, AirborneMeleeSwingState state)
    {
        SwingStates.Remove(attack);
        SwingStates.Add(attack, state);
    }

    private static void EmitStartDecision(
        AirborneMeleeStartIdentity attempt,
        string eventName,
        string reason,
        bool nativeAttackStarted,
        float? currentVerticalSpeed = null)
    {
        DiagnosticEvent diagnosticEvent = DiagnosticEvent.Create("WeaponRhythm", eventName)
            .String("operation_id", attempt.OperationId)
            .String("operation_phase", eventName == "airborne_melee_armed" ? "start" : "terminal")
            .String("reason", reason)
            .String("weapon", attempt.Weapon)
            .String("attack_control", attempt.AttackControl)
            .String("attack_animation", attempt.AttackAnimation)
            .String("attack_type", attempt.AttackType)
            .Boolean("native_attack_started", nativeAttackStarted)
            .Boolean("start_grounded", attempt.StartedGrounded)
            .Number("start_vertical_speed", attempt.StartVerticalSpeed)
            .Number("start_forward_speed", attempt.StartForwardSpeed)
            .Number("forward_speed_threshold", AirborneMeleeTuning.ForwardSpeedThreshold)
            .Number("damage_multiplier", AirborneMeleeTuning.DamageMultiplier)
            .Number("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier)
            .String("feedback", "not_requested");
        if (currentVerticalSpeed.HasValue)
        {
            diagnosticEvent.Number("vertical_speed", currentVerticalSpeed.Value);
        }
        Diagnostics.Emit(diagnosticEvent);
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
