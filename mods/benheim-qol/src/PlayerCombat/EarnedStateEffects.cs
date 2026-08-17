using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.PlayerCombat;

internal sealed class EarnedStateEffectDefinition
{
    internal EarnedStateEffectDefinition(
        EarnedCombatState state,
        int tier,
        EarnedStateStatusEffect effect,
        string activationMessage)
    {
        if (tier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tier));
        }

        State = state;
        Tier = tier;
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        ActivationMessage = activationMessage
            ?? throw new ArgumentNullException(nameof(activationMessage));
    }

    internal EarnedCombatState State { get; }
    internal int Tier { get; }
    internal EarnedStateStatusEffect Effect { get; }
    internal string ActivationMessage { get; }
}

/// <summary>
/// A native SE_Stats with enough identity to close the controller lifecycle
/// when Valheim expires or otherwise stops the cloned effect.
/// </summary>
internal sealed class EarnedStateStatusEffect : SE_Stats
{
    internal EarnedCombatState State { get; set; }
    internal int Tier { get; set; }

    public override void Stop()
    {
        base.Stop();
        if (m_character is Player player)
        {
            PlayerCombatRuntime.ObserveEffectStopped(player, State, Tier);
        }
    }
}

internal sealed class EarnedStateEffectCatalog
{
    private readonly Dictionary<EffectKey, EarnedStateEffectDefinition> definitions =
        new Dictionary<EffectKey, EarnedStateEffectDefinition>();
    private ObjectDB? registeredDatabase;

    internal void Configure(params EarnedStateEffectDefinition[] configuredDefinitions)
    {
        if (configuredDefinitions == null)
        {
            throw new ArgumentNullException(nameof(configuredDefinitions));
        }

        if (registeredDatabase != null)
        {
            throw new InvalidOperationException("Unregister earned-state effects before reconfiguration.");
        }

        definitions.Clear();
        for (int index = 0; index < configuredDefinitions.Length; index++)
        {
            EarnedStateEffectDefinition definition = configuredDefinitions[index]
                ?? throw new ArgumentException("Effect definitions cannot contain null.");
            EffectKey key = new EffectKey(definition.State, definition.Tier);
            if (definitions.ContainsKey(key))
            {
                throw new ArgumentException(
                    $"Duplicate effect definition for {definition.State} tier {definition.Tier}.");
            }

            definition.Effect.State = definition.State;
            definition.Effect.Tier = definition.Tier;
            definitions.Add(key, definition);
        }
    }

    internal void Register(ObjectDB database)
    {
        if (database == null)
        {
            throw new ArgumentNullException(nameof(database));
        }

        if (registeredDatabase != null && registeredDatabase != database)
        {
            Unregister();
        }

        registeredDatabase = database;
        foreach (EarnedStateEffectDefinition definition in definitions.Values)
        {
            StatusEffect? existing = database.GetStatusEffect(definition.Effect.NameHash());
            if (existing == null)
            {
                database.m_StatusEffects.Add(definition.Effect);
            }
            else if (existing != definition.Effect)
            {
                Diagnostics.Event(
                    "PlayerCombat",
                    "earned_state_effect_registration_rejected",
                    $"state={definition.State} tier={definition.Tier} reason=hash_conflict");
            }
        }
    }

    internal void Unregister()
    {
        ObjectDB? database = registeredDatabase;
        registeredDatabase = null;
        if (database == null)
        {
            return;
        }

        foreach (EarnedStateEffectDefinition definition in definitions.Values)
        {
            database.m_StatusEffects.Remove(definition.Effect);
        }
    }

    internal bool TryGet(
        EarnedCombatState state,
        int tier,
        out EarnedStateEffectDefinition definition)
    {
        if (!definitions.TryGetValue(new EffectKey(state, tier), out definition!))
        {
            return false;
        }

        ObjectDB? database = registeredDatabase;
        return database != null
            && database.GetStatusEffect(definition.Effect.NameHash()) == definition.Effect;
    }

    private readonly struct EffectKey : IEquatable<EffectKey>
    {
        internal EffectKey(EarnedCombatState state, int tier)
        {
            State = state;
            Tier = tier;
        }

        private EarnedCombatState State { get; }
        private int Tier { get; }

        public bool Equals(EffectKey other)
        {
            return State == other.State && Tier == other.Tier;
        }

        public override bool Equals(object? obj)
        {
            return obj is EffectKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)State * 397) ^ Tier;
            }
        }
    }
}

internal sealed class NativeEarnedStateOutput : IEarnedStateOutput
{
    private readonly EarnedStateEffectCatalog effects;
    private readonly EarnedStatePresentation presentation;

    internal NativeEarnedStateOutput(
        EarnedStateEffectCatalog effects,
        EarnedStatePresentation presentation)
    {
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
        this.presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
    }

    public bool Activate(Player player, EarnedCombatState state, int tier)
    {
        if (!effects.TryGet(state, tier, out EarnedStateEffectDefinition definition))
        {
            Diagnostics.Event(
                "PlayerCombat",
                "earned_state_activation_rejected",
                $"state={state} tier={tier} reason=effect_unavailable");
            return false;
        }

        int effectHash = definition.Effect.NameHash();
        SEMan statusEffects = player.GetSEMan();
        StatusEffect? applied = statusEffects.AddStatusEffect(effectHash, resetTime: true);
        if (applied == null && !statusEffects.HaveStatusEffect(effectHash))
        {
            Diagnostics.Event(
                "PlayerCombat",
                "earned_state_activation_rejected",
                $"state={state} tier={tier} reason=native_application_failed");
            return false;
        }

        presentation.ShowActivation(definition.ActivationMessage);
        return true;
    }

    public void Deactivate(Player player, EarnedCombatState state, int tier)
    {
        if (effects.TryGet(state, tier, out EarnedStateEffectDefinition definition))
        {
            player.GetSEMan().RemoveStatusEffect(definition.Effect.NameHash(), quiet: true);
        }
    }
}
