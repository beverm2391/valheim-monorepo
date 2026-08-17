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
