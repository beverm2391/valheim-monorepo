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
                bool feedbackShown = ShowPerfectImpactFeedback();
                Diagnostics.Event(
                    "WeaponRhythm",
                    "airborne_melee_applied",
                    $"skill={hit.m_skill} target={TargetName(targetCharacter)} " +
                    $"vertical_speed={verticalSpeed:0.00} " +
                    $"descent_threshold={AirborneMeleeTuning.DescentThreshold:0.00} " +
                    $"toward_target_speed={towardTargetSpeed:0.00} " +
                    $"approach_threshold={AirborneMeleeTuning.ApproachSpeedThreshold:0.00} " +
                    $"damage_multiplier={AirborneMeleeTuning.DamageMultiplier:0.##} " +
                    $"stagger_multiplier={AirborneMeleeTuning.StaggerMultiplier:0.##} " +
                    $"feedback={(feedbackShown ? "shown" : "same_outcome_coalesced")}");
            }
            else
            {
                string reason = grounded
                    ? "grounded"
                    : verticalSpeed > AirborneMeleeTuning.DescentThreshold
                        ? "rising_or_apex"
                        : "insufficient_approach";
                Diagnostics.Event(
                    "WeaponRhythm",
                    "airborne_melee_skipped",
                    $"reason={reason} " +
                    $"skill={hit.m_skill} target={TargetName(targetCharacter)} " +
                    $"vertical_speed={verticalSpeed:0.00} " +
                    $"descent_threshold={AirborneMeleeTuning.DescentThreshold:0.00} " +
                    $"toward_target_speed={towardTargetSpeed:0.00} " +
                    $"approach_threshold={AirborneMeleeTuning.ApproachSpeedThreshold:0.00}");
            }
        }

        target.Damage(hit);
    }

    private static bool ShowPerfectImpactFeedback()
    {
        // A native area or multi-target melee outcome resolves its target
        // contacts synchronously in one frame. Coalescing presentation at that
        // boundary keeps the per-target damage truthful without duplicating
        // the player-facing confirmation or shake.
        int currentFrame = Time.frameCount;
        if (lastFeedbackFrame == currentFrame)
        {
            return false;
        }

        lastFeedbackFrame = currentFrame;
        TopLeftFeedbackHud.ShowTransient(SuccessMessage);
        CombatFeedbackController.RequestShake(CombatFeedbackTrigger.PerfectImpact);
        return true;
    }

    private static string TargetName(Character target)
    {
        return Diagnostics.Flatten(target.gameObject.name);
    }
}
