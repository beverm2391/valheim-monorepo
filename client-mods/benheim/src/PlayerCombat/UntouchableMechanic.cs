using UnityEngine;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Owns Ben's current indefinite mixed-defense streak and its three native
/// outgoing-damage tiers. No timer or ordinary combat boundary resets it.
/// </summary>
internal static class UntouchableMechanic
{
    internal const string EffectCategory = "Benheim.EarnedState.Untouchable";
    internal const string DamageIconItemPrefab = "TrinketSilverDamage";
    internal const string DamageIconStatusEffect = "TrinketSilverDamage";

    internal static int TierForStreak(int streak)
    {
        if (streak >= 12)
        {
            return 3;
        }

        if (streak >= 8)
        {
            return 2;
        }

        return streak >= 5 ? 1 : 0;
    }

    internal static float DamageMultiplierForTier(int tier)
    {
        return tier switch
        {
            1 => 1.10f,
            2 => 1.20f,
            3 => 1.30f,
            _ => 1f
        };
    }

    internal static string ActivationTextForTier(int tier)
    {
        return tier switch
        {
            1 => "UNTOUCHABLE!",
            2 => "UNTOUCHABLE II!",
            3 => "UNTOUCHABLE III!",
            _ => string.Empty
        };
    }

    internal static EarnedStateEffectDefinition CreateEffectDefinition(int tier)
    {
        EarnedStateStatusEffect effect =
            ScriptableObject.CreateInstance<EarnedStateStatusEffect>();
        effect.name = $"SE_Benheim_Untouchable_{tier}";
        effect.m_name = tier == 1 ? "UNTOUCHABLE" : $"UNTOUCHABLE {RomanTier(tier)}";
        effect.m_category = EffectCategory;
        effect.m_tooltip = $"Outgoing damage increased by {tier * 10}%.";
        effect.m_ttl = 0f;
        effect.m_modifyAttackSkill = Skills.SkillType.All;
        effect.m_damageModifier = DamageMultiplierForTier(tier);
        effect.m_startMessage = string.Empty;
        effect.m_stopMessage = string.Empty;

        return new EarnedStateEffectDefinition(
            EarnedCombatState.Untouchable,
            tier,
            effect,
            new NativeStatusIconSource(
                DamageIconItemPrefab,
                DamageIconStatusEffect,
                NativeStatusIconSourceKind.FullAdrenalineStatusEffect));
    }

    private static string RomanTier(int tier) => tier == 2 ? "II" : "III";
}
