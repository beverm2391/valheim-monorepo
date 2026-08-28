namespace BenheimQoL.EnemyTiers;

internal static class BoarTierCombat
{
    private const string BoarPrefabName = "Boar";

    internal static void AdjustIncomingPush(Character target, ref float pushForce)
    {
        if (TryGetProfile(target, out BoarTierPhysicalProfile profile))
        {
            pushForce *= profile.IncomingPushMultiplier;
        }
    }

    internal static void AdjustOutgoingPush(Character target, HitData hit)
    {
        if (!target.IsPlayer())
        {
            return;
        }

        Character? attacker = hit.GetAttacker();
        if (TryGetProfile(attacker, out BoarTierPhysicalProfile profile))
        {
            // The HitData belongs to this swing and is serialized after the
            // target's Damage entrypoint. This avoids mutating the Boar's
            // shallow-shared weapon or Attack definition.
            hit.m_pushForce *= profile.OutgoingPushMultiplier;
        }
    }

    internal static void ExtendPursuit(
        MonsterAI monsterAI,
        float dt,
        ref float timeSinceSensedTarget,
        ref float timeSinceAttacking)
    {
        Character? character = monsterAI.GetComponent<Character>();
        if (character == null ||
            !character.IsOwner() ||
            !TryGetProfile(character, out BoarTierPhysicalProfile profile))
        {
            return;
        }

        // These timers can sit at zero while no target exists. Clamp the
        // prefix adjustment there so idle time never becomes negative credit
        // toward a later chase; native UpdateAI then advances active timers.
        timeSinceSensedTarget = profile.CompensatePursuitTimer(timeSinceSensedTarget, dt);
        timeSinceAttacking = profile.CompensatePursuitTimer(timeSinceAttacking, dt);
    }

    private static bool TryGetProfile(
        Character? character,
        out BoarTierPhysicalProfile profile)
    {
        profile = default;
        return character != null &&
            Utils.GetPrefabName(character.gameObject) == BoarPrefabName &&
            BoarTierPhysicalProfile.TryForLevel(character.GetLevel(), out profile);
    }
}
