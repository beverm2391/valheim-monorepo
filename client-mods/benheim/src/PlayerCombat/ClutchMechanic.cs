using UnityEngine;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Owns only Ben's current CLUTCH experiment. The rule consumes the same
/// confirmed perfect-defense fact that later mechanics may also observe.
/// </summary>
internal static class ClutchMechanic
{
    internal const float HealthThreshold = 30f;
    internal const float DurationSeconds = 6f;
    internal const float HealthPerSecond = 10f;
    internal const int Tier = 1;
    internal const string EffectIdentity = "SE_Benheim_Clutch";
    internal const string EffectCategory = "Benheim.EarnedState.Clutch";
    internal const string HealthIconItemPrefab = "MeadHealthLingering";
    internal const string HealthIconStatusEffect = "Potion_health_lingering";
    internal const string ActivationText = "CLUTCH!";

    internal static ClutchDecision Decide(
        PerfectDefenseConfirmed perfectDefense,
        bool isActive)
    {
        bool eligible = perfectDefense.Context.Health < HealthThreshold;
        return new ClutchDecision(
            perfectDefense.Context,
            perfectDefense.Kind,
            HealthThreshold,
            eligible
                ? (isActive ? ClutchDecisionOutcome.Refresh : ClutchDecisionOutcome.Activate)
                : ClutchDecisionOutcome.Reject,
            eligible
                ? ClutchDecisionReason.CriticalHealth
                : ClutchDecisionReason.HealthThresholdNotMet);
    }

    internal static EarnedStateEffectDefinition CreateEffectDefinition()
    {
        EarnedStateStatusEffect effect =
            ScriptableObject.CreateInstance<EarnedStateStatusEffect>();
        effect.name = EffectIdentity;
        effect.m_name = "CLUTCH";
        effect.m_category = EffectCategory;
        effect.m_tooltip = "Recovering 10 health per second.";
        effect.m_ttl = DurationSeconds;
        effect.m_tickInterval = 1f;
        effect.m_healthPerTickMinHealthPercentage = 0f;
        effect.m_healthPerTick = HealthPerSecond;
        effect.m_startMessage = string.Empty;
        effect.m_stopMessage = string.Empty;

        return new EarnedStateEffectDefinition(
            EarnedCombatState.Clutch,
            Tier,
            effect,
            new NativeStatusIconSource(
                HealthIconItemPrefab,
                HealthIconStatusEffect,
                NativeStatusIconSourceKind.ConsumeStatusEffect));
    }
}
