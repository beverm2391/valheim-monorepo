using BenheimQoL.Infrastructure;

namespace BenheimQoL.WeaponRhythm;

internal static class AirborneMelee
{
    internal static void DamageMeleeTarget(IDestructible target, HitData hit)
    {
        Character? targetCharacter = target as Character;
        Player? localPlayer = Player.m_localPlayer;
        Character? attacker = hit.GetAttacker();
        bool localAttack = localPlayer != null && attacker == localPlayer;
        bool airborne = localAttack && !localPlayer!.IsOnGround();

        if (localAttack && targetCharacter != null && !airborne)
        {
            Diagnostics.Event(
                "WeaponRhythm",
                "airborne_melee_skipped",
                $"reason=grounded skill={hit.m_skill} target={TargetName(targetCharacter)}");
        }
        else if (AirborneMeleeRules.Qualifies(
            targetCharacter != null,
            localAttack,
            airborne))
        {
            hit.m_damage.Modify(AirborneMeleeTuning.DamageMultiplier);
            hit.m_staggerMultiplier *= AirborneMeleeTuning.StaggerMultiplier;
            Diagnostics.Event(
                "WeaponRhythm",
                "airborne_melee_applied",
                $"skill={hit.m_skill} target={TargetName(targetCharacter!)} " +
                $"damage_multiplier={AirborneMeleeTuning.DamageMultiplier:0.##} " +
                $"stagger_multiplier={AirborneMeleeTuning.StaggerMultiplier:0.##}");
        }

        target.Damage(hit);
    }

    private static string TargetName(Character target)
    {
        return Diagnostics.Flatten(target.gameObject.name);
    }
}
