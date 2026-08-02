using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Woodcutting;

internal static class WoodcuttingProgression
{
    private const float CleaveUnlockLevel = 25f;
    private const float MinCleaveChance = 0.3f;
    private const float MaxCleaveChance = 0.85f;
    private const float CleaveDamageMultiplier = 0.5f;

    private static bool cleaveRunning;

    internal static void TryApplyCleave(Component target, HitData hit)
    {
        if (cleaveRunning || !target || !IsLocalWoodcuttingHit(hit))
        {
            return;
        }

        float skillFactor = GetWoodcuttingSkillFactor(hit);
        if (skillFactor * 100f < CleaveUnlockLevel)
        {
            Diagnostics.Event(
                "Woodcutting",
                "cleave_skipped",
                $"reason=below_unlock skill={skillFactor * 100f:0.##} unlock={CleaveUnlockLevel:0.##}");
            return;
        }

        float unlockedFactor = Mathf.InverseLerp(CleaveUnlockLevel / 100f, 1f, skillFactor);
        float chance = Mathf.Lerp(MinCleaveChance, MaxCleaveChance, unlockedFactor);
        float roll = Random.value;
        if (roll > chance)
        {
            Diagnostics.Event(
                "Woodcutting",
                "cleave_skipped",
                $"reason=roll skill={skillFactor * 100f:0.##} chance={chance:0.###} roll={roll:0.###}");
            return;
        }

        HitData cleaveHit = hit.Clone();
        cleaveHit.m_damage.Modify(CleaveDamageMultiplier);
        cleaveHit.m_pushForce = 0f;
        cleaveHit.m_radius = 0f;
        cleaveHit.m_skillRaiseAmount = 0f;

        cleaveRunning = true;
        bool applied = false;
        try
        {
            if (target is TreeBase tree && tree)
            {
                tree.Damage(cleaveHit);
                applied = true;
            }
            else if (target is TreeLog log && log)
            {
                log.Damage(cleaveHit);
                applied = true;
            }

            if (applied)
            {
                WorldFeedback.ShowAt(hit.m_point + Vector3.up * 0.25f, "CLEAVE");
            }
        }
        finally
        {
            Diagnostics.Event(
                "Woodcutting",
                "cleave_finished",
                $"applied={Diagnostics.Bool(applied)} skill={skillFactor * 100f:0.##} chance={chance:0.###} roll={roll:0.###} damage_multiplier={CleaveDamageMultiplier:0.###} target={target.GetType().Name}");
            cleaveRunning = false;
        }
    }

    private static bool IsLocalWoodcuttingHit(HitData hit)
    {
        Player player = Player.m_localPlayer;
        return hit != null
            && player != null
            && hit.m_damage.m_chop > 0f
            && hit.m_attacker == player.GetZDOID();
    }

    private static float GetWoodcuttingSkillFactor(HitData hit)
    {
        Character attacker = hit.GetAttacker();
        if (attacker is Player player)
        {
            return player.GetSkillFactor(Skills.SkillType.WoodCutting);
        }

        return Player.m_localPlayer
            ? Player.m_localPlayer.GetSkillFactor(Skills.SkillType.WoodCutting)
            : 0f;
    }
}
