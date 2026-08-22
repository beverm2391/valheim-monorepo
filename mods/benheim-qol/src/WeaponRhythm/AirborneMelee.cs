using System;
using System.Runtime.CompilerServices;
using BenheimQoL.CombatFeedback;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMelee
{
    private const string SuccessMessage = "PERFECT IMPACT";
    private static readonly ConditionalWeakTable<Attack, AirborneMeleeSwingState> SwingStates = new();
    private static bool optionalOutcomeFailureLogged;

    internal static void ObserveAttackStarted(
        Humanoid character,
        Attack attack,
        bool secondaryAttack)
    {
        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null
            || character != localPlayer
            || !IsMeleeAttack(attack.m_attackType))
        {
            return;
        }

        ItemDrop.ItemData? weapon = localPlayer.GetCurrentWeapon();
        if (weapon == null)
        {
            return;
        }

        ReplaceSwingState(
            attack,
            new AirborneMeleeSwingState(
                Diagnostics.NewOperationId(),
                weapon.m_shared.m_name,
                secondaryAttack ? "secondary" : "primary",
                attack.m_attackAnimation,
                attack.m_attackType.ToString()));
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
            Vector3 towardContact = hit.m_point - localPlayer.transform.position;
            float towardTargetSpeed = AirborneMeleeRules.ProjectPlanarVelocityToward(
                velocity.x,
                velocity.z,
                towardContact.x,
                towardContact.z);
            PerfectImpactResolution resolution = AirborneMeleeRules.ResolveContact(
                attackerIsGrounded: grounded,
                verticalSpeed: velocity.y,
                descentThreshold: AirborneMeleeTuning.DescentThreshold,
                towardTargetSpeed: towardTargetSpeed,
                approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold);
            bool firstResolution = state.TryResolve(resolution);

            // The first Character contact owns the attack's one outcome. A
            // later contact cannot qualify an attack that already stayed
            // native. A qualified multi-target attack still checks the same
            // contact-time physics before modifying each native hit.
            if (state.Qualified && resolution == PerfectImpactResolution.Applied)
            {
                hit.m_damage.Modify(AirborneMeleeTuning.DamageMultiplier);
                hit.m_staggerMultiplier *= AirborneMeleeTuning.StaggerMultiplier;
            }

            if (firstResolution)
            {
                string feedbackResult = state.Qualified ? "error" : "not_requested";
                Action? present = state.Qualified
                    ? () => feedbackResult = ShowPerfectImpactFeedback()
                    : null;
                PerfectImpactOutcomeDelivery.Deliver(
                    present,
                    () => PerfectImpactDiagnostics.Emit(
                        new PerfectImpactOutcome(
                            state.OperationId,
                            resolution,
                            state.Weapon,
                            state.AttackControl,
                            state.AttackAnimation,
                            state.AttackType,
                            hit.m_skill.ToString(),
                            TargetName(targetCharacter),
                            grounded,
                            velocity.y,
                            AirborneMeleeTuning.DescentThreshold,
                            towardTargetSpeed,
                            AirborneMeleeTuning.ApproachSpeedThreshold,
                            AirborneMeleeTuning.DamageMultiplier,
                            AirborneMeleeTuning.StaggerMultiplier,
                            feedbackResult)),
                    () => target.Damage(hit),
                    ReportOptionalOutcomeFailure);
                return;
            }
        }

        target.Damage(hit);
    }

    private static void ReplaceSwingState(Attack attack, AirborneMeleeSwingState state)
    {
        SwingStates.Remove(attack);
        SwingStates.Add(attack, state);
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

    private static void ReportOptionalOutcomeFailure(Exception exception)
    {
        if (optionalOutcomeFailureLogged)
        {
            return;
        }

        optionalOutcomeFailureLogged = true;
        Plugin.Log.LogWarning(
            $"Perfect Impact feedback or diagnostics failed: {Diagnostics.Flatten(exception.Message)}");
    }
}
