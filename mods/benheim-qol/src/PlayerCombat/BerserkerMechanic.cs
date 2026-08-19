using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Maps the server-authoritative kill-chain tier onto the shared native
/// earned-state output. It does not count kills or own chain timing.
/// </summary>
internal static class BerserkerMechanic
{
    internal const float DurationSeconds = 30f;
    internal const string EffectCategory = "Benheim.EarnedState.Berserker";
    internal const string ResistanceIconItemPrefab = "TrinketSilverResist";
    internal const string ResistanceIconStatusEffect = "TrinketSilverResist";

    internal static int TierNumber(BerserkerChainTier tier)
    {
        return tier switch
        {
            BerserkerChainTier.Berserker => 1,
            BerserkerChainTier.Slaughterhouse => 2,
            _ => 0
        };
    }

    internal static string ActivationTextForTier(int tier)
    {
        return tier switch
        {
            1 => "BERSERKER!",
            2 => "SLAUGHTERHOUSE!",
            _ => string.Empty
        };
    }

    internal static EarnedStateEffectDefinition CreateEffectDefinition(int tier)
    {
        HitData.DamageModifier resistance = tier == 1
            ? HitData.DamageModifier.SlightlyResistant
            : HitData.DamageModifier.Resistant;
        EarnedStateStatusEffect effect =
            ScriptableObject.CreateInstance<EarnedStateStatusEffect>();
        effect.name = $"SE_Benheim_Berserker_{tier}";
        effect.m_name = tier == 1 ? "BERSERKER" : "SLAUGHTERHOUSE";
        effect.m_category = EffectCategory;
        effect.m_ttl = DurationSeconds;
        effect.m_staminaRegenMultiplier = tier == 1 ? 1.5f : 2f;
        effect.m_tooltip = tier == 1
            ? "25% less blunt, slash, and pierce damage; 50% more stamina regeneration."
            : "50% less blunt, slash, and pierce damage; 100% more stamina regeneration.";
        effect.m_mods = new List<HitData.DamageModPair>
        {
            DamageResistance(HitData.DamageType.Blunt, resistance),
            DamageResistance(HitData.DamageType.Slash, resistance),
            DamageResistance(HitData.DamageType.Pierce, resistance)
        };
        effect.m_startMessage = string.Empty;
        effect.m_stopMessage = string.Empty;

        return new EarnedStateEffectDefinition(
            EarnedCombatState.Berserker,
            tier,
            effect,
            new NativeStatusIconSource(
                ResistanceIconItemPrefab,
                ResistanceIconStatusEffect,
                NativeStatusIconSourceKind.FullAdrenalineStatusEffect));
    }

    private static HitData.DamageModPair DamageResistance(
        HitData.DamageType type,
        HitData.DamageModifier resistance)
    {
        return new HitData.DamageModPair
        {
            m_type = type,
            m_modifier = resistance
        };
    }
}
