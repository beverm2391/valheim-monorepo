using BenheimQoL.CombatFeedback;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMelee
{
    private const string SuccessMessage = "PERFECT IMPACT";
    private static int lastFeedbackFrame = -1;

    internal static void DamageMeleeTarget(IDestructible target, HitData hit)
    {
        Character? targetCharacter = target as Character;
        Player? localPlayer = Player.m_localPlayer;
        Character? attacker = hit.GetAttacker();
        bool localAttack = localPlayer != null && attacker == localPlayer;

        if (localAttack && targetCharacter != null)
        {
            bool grounded = localPlayer!.IsOnGround();
            Vector3 velocity = localPlayer.GetVelocity();
            float verticalSpeed = velocity.y;
            Vector3 towardContact = hit.m_point - localPlayer.transform.position;
            float towardTargetSpeed = AirborneMeleeRules.ProjectPlanarVelocityToward(
                velocity.x,
                velocity.z,
                towardContact.x,
                towardContact.z);
            if (AirborneMeleeRules.Qualifies(
                targetIsCharacter: true,
                attackerIsLocalPlayer: true,
                attackerIsGrounded: grounded,
                verticalSpeed: verticalSpeed,
                descentThreshold: AirborneMeleeTuning.DescentThreshold,
                towardTargetSpeed: towardTargetSpeed,
                approachSpeedThreshold: AirborneMeleeTuning.ApproachSpeedThreshold))
            {
                hit.m_damage.Modify(AirborneMeleeTuning.DamageMultiplier);
                hit.m_staggerMultiplier *= AirborneMeleeTuning.StaggerMultiplier;
                string feedbackResult = ShowPerfectImpactFeedback();
                Diagnostics.Emit(
                    DiagnosticEvent.Create("WeaponRhythm", "airborne_melee_applied")
                        .String("skill", hit.m_skill.ToString())
                        .String("target", TargetName(targetCharacter))
                        .Number("vertical_speed", verticalSpeed)
                        .Number("descent_threshold", AirborneMeleeTuning.DescentThreshold)
                        .Number("toward_target_speed", towardTargetSpeed)
                        .Number("approach_threshold", AirborneMeleeTuning.ApproachSpeedThreshold)
                        .Number("damage_multiplier", AirborneMeleeTuning.DamageMultiplier)
                        .Number("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier)
                        .String("feedback", feedbackResult));
            }
            else
            {
                string reason = grounded
                    ? "grounded"
                    : verticalSpeed > AirborneMeleeTuning.DescentThreshold
                        ? "rising_or_apex"
                        : "insufficient_approach";
                Diagnostics.Emit(
                    DiagnosticEvent.Create("WeaponRhythm", "airborne_melee_skipped")
                        .String("reason", reason)
                        .String("skill", hit.m_skill.ToString())
                        .String("target", TargetName(targetCharacter))
                        .Number("vertical_speed", verticalSpeed)
                        .Number("descent_threshold", AirborneMeleeTuning.DescentThreshold)
                        .Number("toward_target_speed", towardTargetSpeed)
                        .Number("approach_threshold", AirborneMeleeTuning.ApproachSpeedThreshold)
                        .Number("damage_multiplier", AirborneMeleeTuning.DamageMultiplier)
                        .Number("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier)
                        .String("feedback", "not_requested"));
            }
        }

        target.Damage(hit);
    }

    private static string ShowPerfectImpactFeedback()
    {
        // A native area or multi-target melee outcome resolves its target
        // contacts synchronously in one frame. Coalescing presentation at that
        // boundary keeps the per-target damage truthful without duplicating
        // the player-facing confirmation or shake.
        int currentFrame = Time.frameCount;
        if (lastFeedbackFrame == currentFrame)
        {
            return "same_outcome_coalesced";
        }

        lastFeedbackFrame = currentFrame;
        TopLeftFeedbackResult feedbackResult = TopLeftFeedbackHud.ShowTransient(SuccessMessage);
        CombatFeedbackController.RequestShake(CombatFeedbackTrigger.PerfectImpact);
        return feedbackResult switch
        {
            TopLeftFeedbackResult.Placed => "placed",
            TopLeftFeedbackResult.CreatedNotPlaced => "created_not_placed",
            _ => "unavailable"
        };
    }

    private static string TargetName(Character target)
    {
        return Diagnostics.Flatten(target.gameObject.name);
    }
}
