using System;
using UnityEngine;

namespace BenheimQoL.PlayerCombat;

internal enum PerfectDefenseKind
{
    Parry,
    Dodge
}

internal enum PlayerCombatEndReason
{
    Death,
    PlayerDestroyed,
    WorldTeardown,
    PluginTeardown
}

internal enum EarnedCombatState
{
    Clutch,
    Untouchable,
    Berserker
}

internal enum ClutchDecisionOutcome
{
    Activate,
    Refresh,
    Reject
}

internal enum ClutchDecisionReason
{
    CriticalHealth,
    HealthThresholdNotMet
}

internal enum EarnedStateTransitionKind
{
    Activated,
    Refreshed,
    Expired,
    Removed,
    Rejected
}

internal enum EarnedStateTransitionReason
{
    NativeEffectApplied,
    NativeEffectRefreshed,
    NativeDurationElapsed,
    AcceptedDamage,
    TierReplaced,
    LifecycleReset,
    ServerChainExpired,
    ServerChainAlreadyExpired,
    EffectUnavailable,
    NativeApplicationFailed,
    NativeHudPresenceFailed
}

internal enum UntouchableProgressOutcome
{
    StreakIncremented,
    TierActivated,
    TierEscalated
}

internal enum UntouchableResetReason
{
    AcceptedDamage
}

internal enum BerserkerChainTransitionKind
{
    Progressed,
    Activated,
    Refreshed,
    Escalated,
    Expired
}

internal enum BerserkerChainTier
{
    None,
    Berserker,
    Slaughterhouse
}

/// <summary>
/// Captures the player facts that were true when a combat event occurred.
/// The Player reference identifies the ephemeral controller; health values are
/// snapshots and must not be recomputed when a subscriber handles the event.
/// </summary>
internal sealed class PlayerCombatContext
{
    internal PlayerCombatContext(Player player, float health, float maximumHealth)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Health = health;
        MaximumHealth = maximumHealth;
    }

    internal Player Player { get; }
    internal float Health { get; }
    internal float MaximumHealth { get; }
    internal float HealthFraction => MaximumHealth > 0f ? Health / MaximumHealth : 0f;

    internal static PlayerCombatContext Capture(Player player)
    {
        return new PlayerCombatContext(player, player.GetHealth(), player.GetMaxHealth());
    }
}

internal sealed class PerfectDefenseConfirmed
{
    internal PerfectDefenseConfirmed(
        PlayerCombatContext context,
        PerfectDefenseKind kind,
        float? blockTimer = null,
        float? timedBlockBonus = null)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Kind = kind;
        BlockTimer = blockTimer;
        TimedBlockBonus = timedBlockBonus;
    }

    internal PlayerCombatContext Context { get; }
    internal PerfectDefenseKind Kind { get; }
    internal float? BlockTimer { get; }
    internal float? TimedBlockBonus { get; }
}

/// <summary>
/// Records the CLUTCH rule's decision from the immutable perfect-defense
/// snapshot. Native output happens only after an eligible decision.
/// </summary>
internal sealed class ClutchDecision
{
    internal ClutchDecision(
        PlayerCombatContext context,
        PerfectDefenseKind defense,
        float healthThreshold,
        ClutchDecisionOutcome outcome,
        ClutchDecisionReason reason)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Defense = defense;
        HealthThreshold = healthThreshold;
        Outcome = outcome;
        Reason = reason;
    }

    internal PlayerCombatContext Context { get; }
    internal PerfectDefenseKind Defense { get; }
    internal float HealthThreshold { get; }
    internal ClutchDecisionOutcome Outcome { get; }
    internal ClutchDecisionReason Reason { get; }
}

/// <summary>
/// Records the accepted native-output lifecycle for an earned combat state.
/// Presentation and diagnostics project this fact but do not decide gameplay.
/// </summary>
internal sealed class EarnedStateTransition
{
    internal EarnedStateTransition(
        PlayerCombatContext context,
        EarnedCombatState state,
        int tier,
        EarnedStateTransitionKind kind,
        EarnedStateTransitionReason reason)
    {
        if (tier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tier));
        }

        Context = context ?? throw new ArgumentNullException(nameof(context));
        State = state;
        Tier = tier;
        Kind = kind;
        Reason = reason;
    }

    internal PlayerCombatContext Context { get; }
    internal EarnedCombatState State { get; }
    internal int Tier { get; }
    internal EarnedStateTransitionKind Kind { get; }
    internal EarnedStateTransitionReason Reason { get; }
}

/// <summary>
/// One mixed parry-and-dodge streak update and any tier decision it makes.
/// The event is emitted once per confirmed defense without per-frame traffic.
/// </summary>
internal sealed class UntouchableProgress
{
    internal UntouchableProgress(
        PlayerCombatContext context,
        PerfectDefenseKind defense,
        int previousStreak,
        int currentStreak,
        int previousTier,
        int currentTier,
        UntouchableProgressOutcome outcome)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Defense = defense;
        PreviousStreak = previousStreak;
        CurrentStreak = currentStreak;
        PreviousTier = previousTier;
        CurrentTier = currentTier;
        Outcome = outcome;
    }

    internal PlayerCombatContext Context { get; }
    internal PerfectDefenseKind Defense { get; }
    internal int PreviousStreak { get; }
    internal int CurrentStreak { get; }
    internal int PreviousTier { get; }
    internal int CurrentTier { get; }
    internal UntouchableProgressOutcome Outcome { get; }
}

internal sealed class UntouchableReset
{
    internal UntouchableReset(
        AcceptedPlayerDamage damage,
        int previousStreak,
        int previousTier,
        UntouchableResetReason reason)
    {
        Damage = damage ?? throw new ArgumentNullException(nameof(damage));
        PreviousStreak = previousStreak;
        PreviousTier = previousTier;
        Reason = reason;
    }

    internal AcceptedPlayerDamage Damage { get; }
    internal int PreviousStreak { get; }
    internal int PreviousTier { get; }
    internal UntouchableResetReason Reason { get; }
}

/// <summary>
/// One authoritative server-chain transition delivered to the validated local
/// killer. The server owns chain timing; the native client duration is only an
/// output/countdown safety bound.
/// </summary>
internal sealed class BerserkerChainTransition
{
    internal BerserkerChainTransition(
        PlayerCombatContext context,
        BerserkerChainTransitionKind kind,
        BerserkerChainTier tier,
        int killCount,
        long serverSequence,
        double serverTimeSeconds,
        double expiresAtServerTimeSeconds)
    {
        if (killCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(killCount));
        }

        if (serverSequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(serverSequence));
        }

        if (double.IsNaN(serverTimeSeconds)
            || double.IsInfinity(serverTimeSeconds)
            || double.IsNaN(expiresAtServerTimeSeconds)
            || double.IsInfinity(expiresAtServerTimeSeconds))
        {
            throw new ArgumentException("Chain transition times must be finite.");
        }

        bool active = kind == BerserkerChainTransitionKind.Activated
            || kind == BerserkerChainTransitionKind.Refreshed
            || kind == BerserkerChainTransitionKind.Escalated;
        bool terminal = kind == BerserkerChainTransitionKind.Expired;
        bool validShape = kind switch
        {
            BerserkerChainTransitionKind.Progressed =>
                tier == BerserkerChainTier.None && killCount is >= 1 and <= 2,
            BerserkerChainTransitionKind.Activated =>
                tier == BerserkerChainTier.Berserker && killCount == 3,
            BerserkerChainTransitionKind.Refreshed =>
                (tier == BerserkerChainTier.Berserker && killCount is >= 4 and <= 5)
                || (tier == BerserkerChainTier.Slaughterhouse && killCount > 6),
            BerserkerChainTransitionKind.Escalated =>
                tier == BerserkerChainTier.Slaughterhouse && killCount == 6,
            BerserkerChainTransitionKind.Expired =>
                tier == BerserkerChainTier.None && killCount == 0,
            _ => false
        };
        if (!validShape)
        {
            throw new ArgumentException("Chain transition kind, tier, and count are inconsistent.");
        }

        if (active && expiresAtServerTimeSeconds <= serverTimeSeconds)
        {
            throw new ArgumentException("Active chain transitions require a future server expiry.");
        }

        if ((kind == BerserkerChainTransitionKind.Progressed
                && expiresAtServerTimeSeconds <= serverTimeSeconds)
            || (terminal && expiresAtServerTimeSeconds != 0d))
        {
            throw new ArgumentException("Chain transition expiry is inconsistent with its lifecycle.");
        }

        Context = context ?? throw new ArgumentNullException(nameof(context));
        Kind = kind;
        Tier = tier;
        KillCount = killCount;
        ServerSequence = serverSequence;
        ServerTimeSeconds = serverTimeSeconds;
        ExpiresAtServerTimeSeconds = expiresAtServerTimeSeconds;
    }

    internal PlayerCombatContext Context { get; }
    internal BerserkerChainTransitionKind Kind { get; }
    internal BerserkerChainTier Tier { get; }
    internal int KillCount { get; }
    internal long ServerSequence { get; }
    internal double ServerTimeSeconds { get; }
    internal double ExpiresAtServerTimeSeconds { get; }
    internal float RemainingDurationSeconds(double currentServerTimeSeconds)
    {
        if (double.IsNaN(currentServerTimeSeconds)
            || double.IsInfinity(currentServerTimeSeconds))
        {
            throw new ArgumentException("Current server time must be finite.");
        }

        return (float)Math.Max(0d, ExpiresAtServerTimeSeconds - currentServerTimeSeconds);
    }
}

internal sealed class AcceptedPlayerDamage
{
    internal AcceptedPlayerDamage(
        PlayerCombatContext before,
        PlayerCombatContext after)
    {
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        if (before.Player != after.Player)
        {
            throw new ArgumentException("Damage contexts must identify the same player.");
        }
    }

    internal PlayerCombatContext Before { get; }
    internal PlayerCombatContext After { get; }
    internal float HealthLost => Math.Max(0f, Before.Health - After.Health);
}

internal sealed class PlayerCombatEnded
{
    internal PlayerCombatEnded(Player player, PlayerCombatEndReason reason)
    {
        Player = player ?? throw new ArgumentNullException(nameof(player));
        Reason = reason;
    }

    internal Player Player { get; }
    internal PlayerCombatEndReason Reason { get; }
}

internal sealed class PlayerCombatSessionEnded
{
    internal PlayerCombatSessionEnded(PlayerCombatEndReason reason)
    {
        if (reason != PlayerCombatEndReason.WorldTeardown
            && reason != PlayerCombatEndReason.PluginTeardown)
        {
            throw new ArgumentException("Session reset requires a session-level reason.");
        }

        Reason = reason;
    }

    internal PlayerCombatEndReason Reason { get; }
}

/// <summary>
/// A server-validated direct kill delivered only to the confirmed local killer.
/// Server sequence and time are gameplay facts; transport correlation is not.
/// </summary>
internal sealed class ConfirmedKill
{
    internal ConfirmedKill(
        PlayerCombatContext killer,
        ZDOID killerId,
        ZDOID victimId,
        string victimPrefabName,
        int victimPrefabHash,
        int victimLevel,
        bool victimWasBoss,
        bool victimWasTamed,
        Vector3 killPosition,
        long serverSequence,
        double serverTimeSeconds)
    {
        Killer = killer ?? throw new ArgumentNullException(nameof(killer));
        KillerId = killerId;
        VictimId = victimId;
        VictimPrefabName = victimPrefabName
            ?? throw new ArgumentNullException(nameof(victimPrefabName));
        VictimPrefabHash = victimPrefabHash;
        VictimLevel = victimLevel;
        VictimWasBoss = victimWasBoss;
        VictimWasTamed = victimWasTamed;
        KillPosition = killPosition;
        ServerSequence = serverSequence;
        ServerTimeSeconds = serverTimeSeconds;
    }

    internal PlayerCombatContext Killer { get; }
    internal ZDOID KillerId { get; }
    internal ZDOID VictimId { get; }
    internal string VictimPrefabName { get; }
    internal int VictimPrefabHash { get; }
    internal int VictimLevel { get; }
    internal bool VictimWasBoss { get; }
    internal bool VictimWasTamed { get; }
    internal Vector3 KillPosition { get; }
    internal long ServerSequence { get; }
    internal double ServerTimeSeconds { get; }
}
