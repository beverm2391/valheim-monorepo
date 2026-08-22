using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.PlayerCombat;

internal enum NativeStatusIconSourceKind
{
    ConsumeStatusEffect,
    FullAdrenalineStatusEffect
}

internal sealed class NativeStatusIconSource
{
    internal NativeStatusIconSource(
        string itemPrefab,
        string statusEffectIdentity,
        NativeStatusIconSourceKind kind)
    {
        ItemPrefab = itemPrefab ?? throw new ArgumentNullException(nameof(itemPrefab));
        StatusEffectIdentity = statusEffectIdentity
            ?? throw new ArgumentNullException(nameof(statusEffectIdentity));
        Kind = kind;
    }

    internal string ItemPrefab { get; }
    internal string StatusEffectIdentity { get; }
    internal NativeStatusIconSourceKind Kind { get; }
}

internal sealed class EarnedStateEffectDefinition
{
    internal EarnedStateEffectDefinition(
        EarnedCombatState state,
        int tier,
        EarnedStateStatusEffect effect,
        NativeStatusIconSource iconSource)
    {
        if (tier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tier));
        }

        State = state;
        Tier = tier;
        Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        IconSource = iconSource ?? throw new ArgumentNullException(nameof(iconSource));
    }

    internal EarnedCombatState State { get; }
    internal int Tier { get; }
    internal EarnedStateStatusEffect Effect { get; }
    internal NativeStatusIconSource IconSource { get; }
}

/// <summary>
/// A native SE_Stats with enough identity to close the controller lifecycle
/// when Valheim expires or otherwise stops the cloned effect.
/// </summary>
internal sealed class EarnedStateStatusEffect : SE_Stats
{
    // SE_Stats keeps its tick timer private. This mirror follows the same
    // update rule only so telemetry can observe the native healing callback's
    // input and resolved result. Native ResetTime does not reset its tick
    // timer, so this timer also continues across a CLUTCH refresh.
    private float healthTickTelemetryTimer;

    // Setup runs for each cloned native effect activation. A same-tier refresh
    // calls ResetTime on that clone instead, so high-frequency payload hooks
    // remain bounded to their first application for the active tier.
    private bool outgoingDamageTelemetryRecorded;
    private bool staminaRegenTelemetryRecorded;
    private bool physicalResistanceTelemetryRecorded;

    internal EarnedCombatState State { get; set; }
    internal int Tier { get; set; }

    public override void Setup(Character character)
    {
        healthTickTelemetryTimer = 0f;
        outgoingDamageTelemetryRecorded = false;
        staminaRegenTelemetryRecorded = false;
        physicalResistanceTelemetryRecorded = false;
        base.Setup(character);
    }

    public override void UpdateStatusEffect(float dt)
    {
        bool healingTickDue = false;
        float healthBefore = 0f;
        float maximumHealth = 0f;
        if (State == EarnedCombatState.Clutch
            && m_tickInterval > 0f
            && m_character is Player player)
        {
            healthTickTelemetryTimer += dt;
            if (healthTickTelemetryTimer >= m_tickInterval)
            {
                healthTickTelemetryTimer = 0f;
                healthBefore = player.GetHealth();
                maximumHealth = player.GetMaxHealth();
                healingTickDue = m_healthPerTick > 0f
                    && player.GetHealthPercentage() >= m_healthPerTickMinHealthPercentage;
            }
        }

        base.UpdateStatusEffect(dt);
        if (healingTickDue && m_character is Player healedPlayer)
        {
            TryEmitPayload(
                DiagnosticEvent.Create("PlayerCombat", "earned_state_healing_tick")
                    .String("state", State.ToString())
                    .Integer("tier", Tier)
                    .Number("native_health_before", healthBefore)
                    .Number("native_maximum_health", maximumHealth)
                    .Number("configured_health_per_tick", m_healthPerTick)
                    .Number("configured_tick_interval_seconds", m_tickInterval)
                    .Number("resolved_health_after", healedPlayer.GetHealth())
                    .Number("resolved_health_applied", healedPlayer.GetHealth() - healthBefore));
        }
    }

    public override void ModifyAttack(Skills.SkillType skill, ref HitData hitData)
    {
        HitData.DamageTypes nativeInput = hitData.m_damage;
        base.ModifyAttack(skill, ref hitData);
        if (State != EarnedCombatState.Untouchable
            || outgoingDamageTelemetryRecorded
            || !DamageChanged(nativeInput, hitData.m_damage))
        {
            return;
        }

        outgoingDamageTelemetryRecorded = true;
        DiagnosticEvent diagnosticEvent =
            DiagnosticEvent.Create("PlayerCombat", "earned_state_outgoing_damage_applied")
                .String("state", State.ToString())
                .Integer("tier", Tier)
                .String("skill", skill.ToString())
                .Number("configured_damage_multiplier", m_damageModifier);
        AddDamageFields(diagnosticEvent, "native", nativeInput);
        AddDamageFields(diagnosticEvent, "resolved", hitData.m_damage);
        TryEmitPayload(diagnosticEvent);
    }

    public override void ModifyStaminaRegen(ref float staminaRegen)
    {
        float nativeInput = staminaRegen;
        base.ModifyStaminaRegen(ref staminaRegen);
        if (State != EarnedCombatState.Berserker || staminaRegenTelemetryRecorded)
        {
            return;
        }

        staminaRegenTelemetryRecorded = true;
        TryEmitPayload(
            DiagnosticEvent.Create("PlayerCombat", "earned_state_stamina_regen_applied")
                .String("state", State.ToString())
                .Integer("tier", Tier)
                .Number("native_regen_multiplier", nativeInput)
                .Number("configured_regen_multiplier", m_staminaRegenMultiplier)
                .Number("resolved_regen_multiplier", staminaRegen));
    }

    public override void ModifyDamageMods(ref HitData.DamageModifiers modifiers)
    {
        HitData.DamageModifiers nativeInput = modifiers;
        base.ModifyDamageMods(ref modifiers);
        if (State != EarnedCombatState.Berserker || physicalResistanceTelemetryRecorded)
        {
            return;
        }

        physicalResistanceTelemetryRecorded = true;
        TryEmitPayload(
            DiagnosticEvent.Create("PlayerCombat", "earned_state_physical_resistance_applied")
                .String("state", State.ToString())
                .Integer("tier", Tier)
                .String("native_blunt_modifier", nativeInput.m_blunt.ToString())
                .String("native_slash_modifier", nativeInput.m_slash.ToString())
                .String("native_pierce_modifier", nativeInput.m_pierce.ToString())
                .String(
                    "configured_blunt_modifier",
                    ConfiguredModifier(HitData.DamageType.Blunt).ToString())
                .String(
                    "configured_slash_modifier",
                    ConfiguredModifier(HitData.DamageType.Slash).ToString())
                .String(
                    "configured_pierce_modifier",
                    ConfiguredModifier(HitData.DamageType.Pierce).ToString())
                .String("resolved_blunt_modifier", modifiers.m_blunt.ToString())
                .String("resolved_slash_modifier", modifiers.m_slash.ToString())
                .String("resolved_pierce_modifier", modifiers.m_pierce.ToString()));
    }

    public override void Stop()
    {
        bool expired = IsDone();
        base.Stop();
        if (m_character is Player player)
        {
            PlayerCombatRuntime.ObserveEffectStopped(player, State, Tier, expired);
        }
    }

    private HitData.DamageModifier ConfiguredModifier(HitData.DamageType damageType)
    {
        for (int index = 0; index < m_mods.Count; index++)
        {
            if (m_mods[index].m_type == damageType)
            {
                return m_mods[index].m_modifier;
            }
        }

        return HitData.DamageModifier.Normal;
    }

    private static bool DamageChanged(
        HitData.DamageTypes nativeInput,
        HitData.DamageTypes resolvedOutput)
    {
        return nativeInput.m_damage != resolvedOutput.m_damage
            || nativeInput.m_blunt != resolvedOutput.m_blunt
            || nativeInput.m_slash != resolvedOutput.m_slash
            || nativeInput.m_pierce != resolvedOutput.m_pierce
            || nativeInput.m_chop != resolvedOutput.m_chop
            || nativeInput.m_pickaxe != resolvedOutput.m_pickaxe
            || nativeInput.m_fire != resolvedOutput.m_fire
            || nativeInput.m_frost != resolvedOutput.m_frost
            || nativeInput.m_lightning != resolvedOutput.m_lightning
            || nativeInput.m_poison != resolvedOutput.m_poison
            || nativeInput.m_spirit != resolvedOutput.m_spirit;
    }

    private static void AddDamageFields(
        DiagnosticEvent diagnosticEvent,
        string prefix,
        HitData.DamageTypes damage)
    {
        diagnosticEvent
            .Number($"{prefix}_damage_total", damage.GetTotalDamage())
            .Number($"{prefix}_damage_generic", damage.m_damage)
            .Number($"{prefix}_damage_blunt", damage.m_blunt)
            .Number($"{prefix}_damage_slash", damage.m_slash)
            .Number($"{prefix}_damage_pierce", damage.m_pierce)
            .Number($"{prefix}_damage_chop", damage.m_chop)
            .Number($"{prefix}_damage_pickaxe", damage.m_pickaxe)
            .Number($"{prefix}_damage_fire", damage.m_fire)
            .Number($"{prefix}_damage_frost", damage.m_frost)
            .Number($"{prefix}_damage_lightning", damage.m_lightning)
            .Number($"{prefix}_damage_poison", damage.m_poison)
            .Number($"{prefix}_damage_spirit", damage.m_spirit);
    }

    private static void TryEmitPayload(DiagnosticEvent diagnosticEvent)
    {
        try
        {
            Diagnostics.Emit(diagnosticEvent);
        }
        catch
        {
            // Payload telemetry cannot interrupt native healing, attack,
            // stamina regeneration, or resistance resolution.
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

        DestroyDefinitions();
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
            if (!TryResolveIcon(database, definition, out string iconFailure))
            {
                database.m_StatusEffects.Remove(definition.Effect);
                EmitRegistrationRejected(definition, iconFailure);
                continue;
            }

            StatusEffect? existing = database.GetStatusEffect(definition.Effect.NameHash());
            if (existing == null)
            {
                database.m_StatusEffects.Add(definition.Effect);
                EmitStatus(
                    definition,
                    "registration",
                    "registered",
                    "current_objectdb_icon_resolved");
            }
            else if (existing != definition.Effect)
            {
                EmitRegistrationRejected(definition, "hash_conflict");
            }
            else
            {
                EmitStatus(
                    definition,
                    "registration",
                    "registered",
                    "already_registered");
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

    internal void Reset()
    {
        Unregister();
        DestroyDefinitions();
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
            && definition.Effect.m_icon != null
            && database.GetStatusEffect(definition.Effect.NameHash()) == definition.Effect;
    }

    private static bool TryResolveIcon(
        ObjectDB database,
        EarnedStateEffectDefinition definition,
        out string failure)
    {
        NativeStatusIconSource source = definition.IconSource;
        GameObject? iconPrefab = database.GetItemPrefab(source.ItemPrefab);
        if (iconPrefab == null || iconPrefab.name != source.ItemPrefab)
        {
            failure = "icon_prefab_missing";
            return false;
        }

        ItemDrop? item = iconPrefab.GetComponent<ItemDrop>();
        if (item == null)
        {
            failure = "icon_item_missing";
            return false;
        }

        StatusEffect? nativeEffect = source.Kind == NativeStatusIconSourceKind.ConsumeStatusEffect
            ? item.m_itemData.m_shared.m_consumeStatusEffect
            : item.m_itemData.m_shared.m_fullAdrenalineSE;
        if (nativeEffect == null || nativeEffect.name != source.StatusEffectIdentity)
        {
            failure = "icon_status_effect_missing";
            return false;
        }

        if (nativeEffect.m_icon == null)
        {
            failure = "icon_unavailable";
            return false;
        }

        definition.Effect.m_icon = nativeEffect.m_icon;
        failure = string.Empty;
        return true;
    }

    private static void EmitRegistrationRejected(
        EarnedStateEffectDefinition definition,
        string reason)
    {
        try
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("PlayerCombat", "earned_state_status")
                    .String("operation_phase", "registration")
                    .String("status", "rejected")
                    .String("state", definition.State.ToString())
                    .Integer("tier", definition.Tier)
                    .String("reason", reason)
                    .String("icon_prefab", definition.IconSource.ItemPrefab)
                    .String("icon_status_effect", definition.IconSource.StatusEffectIdentity));
        }
        catch
        {
            // A diagnostic failure cannot turn an unavailable optional effect
            // into a plugin startup failure.
        }
    }

    private static void EmitStatus(
        EarnedStateEffectDefinition definition,
        string phase,
        string status,
        string reason)
    {
        try
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("PlayerCombat", "earned_state_status")
                    .String("operation_phase", phase)
                    .String("status", status)
                    .String("reason", reason)
                    .String("state", definition.State.ToString())
                    .Integer("tier", definition.Tier)
                    .String("icon_prefab", definition.IconSource.ItemPrefab)
                    .String("icon_status_effect", definition.IconSource.StatusEffectIdentity)
                    .String("icon_sprite", definition.Effect.m_icon?.name ?? string.Empty));
        }
        catch
        {
            // Diagnostics cannot interrupt ObjectDB lifecycle registration.
        }
    }

    private void DestroyDefinitions()
    {
        foreach (EarnedStateEffectDefinition definition in definitions.Values)
        {
            UnityEngine.Object.Destroy(definition.Effect);
        }

        definitions.Clear();
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

    internal NativeEarnedStateOutput(EarnedStateEffectCatalog effects)
    {
        this.effects = effects ?? throw new ArgumentNullException(nameof(effects));
    }

    public EarnedStateOutputResult Activate(
        Player player,
        EarnedCombatState state,
        int tier,
        float? durationSeconds = null)
    {
        if (durationSeconds.HasValue && durationSeconds.Value <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        if (!effects.TryGet(state, tier, out EarnedStateEffectDefinition definition))
        {
            return EarnedStateOutputResult.Rejected(
                EarnedStateTransitionReason.EffectUnavailable);
        }

        int effectHash = definition.Effect.NameHash();
        SEMan statusEffects = player.GetSEMan();
        bool wasActive = statusEffects.HaveStatusEffect(effectHash);
        statusEffects.AddStatusEffect(effectHash, resetTime: true);
        StatusEffect? activeEffect = statusEffects.GetStatusEffect(effectHash);
        if (activeEffect == null)
        {
            EmitStatus(
                definition,
                "application",
                "rejected",
                "native_application_failed");
            return EarnedStateOutputResult.Rejected(
                EarnedStateTransitionReason.NativeApplicationFailed);
        }

        if (durationSeconds.HasValue)
        {
            activeEffect.m_ttl = durationSeconds.Value;
        }

        EmitStatus(
            definition,
            "application",
            wasActive ? "refreshed" : "applied",
            "native_status_effect_present");

        List<StatusEffect> hudEffects = new List<StatusEffect>();
        statusEffects.GetHUDStatusEffects(hudEffects);
        bool visibleInNativeHud = activeEffect.m_icon != null
            && hudEffects.Contains(activeEffect);
        if (!visibleInNativeHud)
        {
            statusEffects.RemoveStatusEffect(effectHash, quiet: true);
            EmitStatus(
                definition,
                "presence",
                "rejected",
                activeEffect.m_icon == null
                    ? "active_icon_missing"
                    : "native_hud_list_missing");
            return EarnedStateOutputResult.Rejected(
                EarnedStateTransitionReason.NativeHudPresenceFailed);
        }

        EmitStatus(
            definition,
            "presence",
            "present",
            "native_hud_list_contains_effect");

        return wasActive
            ? EarnedStateOutputResult.Refreshed()
            : EarnedStateOutputResult.Activated();
    }

    public void Deactivate(Player player, EarnedCombatState state, int tier)
    {
        if (effects.TryGet(state, tier, out EarnedStateEffectDefinition definition))
        {
            bool removed = player.GetSEMan().RemoveStatusEffect(
                definition.Effect.NameHash(),
                quiet: true);
            EmitStatus(
                definition,
                "removal",
                removed ? "removed" : "absent",
                "controller_requested");
        }
    }

    private static void EmitStatus(
        EarnedStateEffectDefinition definition,
        string phase,
        string status,
        string reason)
    {
        try
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("PlayerCombat", "earned_state_status")
                    .String("operation_phase", phase)
                    .String("status", status)
                    .String("reason", reason)
                    .String("state", definition.State.ToString())
                    .Integer("tier", definition.Tier)
                    .String("effect", definition.Effect.name));
        }
        catch
        {
            // Native status application and cleanup cannot depend on telemetry.
        }
    }
}
