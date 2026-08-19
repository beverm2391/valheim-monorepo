using BenheimQoL.KillAttribution;
using System;
using System.Collections.Generic;

namespace BenheimServerSupport;

/// <summary>
/// Owns the ephemeral server chain for each confirmed killer. The deadline is
/// rolling: every qualifying kill advances by exactly one and replaces the
/// previous deadline with thirty seconds after that kill.
/// </summary>
internal sealed class KillChainState<TKiller>
    where TKiller : notnull
{
    private readonly object sync = new object();
    private readonly Dictionary<TKiller, ActiveChain> chains =
        new Dictionary<TKiller, ActiveChain>();

    internal KillChainTransition<TKiller> Advance(
        TKiller killer,
        long serverSequence,
        double serverTimeSeconds)
    {
        if (serverSequence < 1L)
        {
            throw new ArgumentOutOfRangeException(nameof(serverSequence));
        }

        if (!FiniteNonNegative(serverTimeSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(serverTimeSeconds));
        }

        lock (sync)
        {
            int count = 1;
            if (chains.TryGetValue(killer, out ActiveChain current)
                && serverTimeSeconds < current.ExpiresAtServerTimeSeconds)
            {
                count = checked(current.KillCount + 1);
            }

            double expiresAt = serverTimeSeconds
                + KillChainRules.WindowSeconds;
            chains[killer] = new ActiveChain(count, serverSequence, expiresAt);
            return CreateActiveTransition(
                killer,
                count,
                serverSequence,
                serverTimeSeconds,
                expiresAt);
        }
    }

    internal void CollectExpired(
        double serverTimeSeconds,
        List<KillChainTransition<TKiller>> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        results.Clear();
        if (!FiniteNonNegative(serverTimeSeconds))
        {
            return;
        }

        lock (sync)
        {
            foreach (KeyValuePair<TKiller, ActiveChain> pair in chains)
            {
                if (serverTimeSeconds >= pair.Value.ExpiresAtServerTimeSeconds)
                {
                    results.Add(
                        CreateTerminalTransition(
                            pair.Key,
                            pair.Value.ServerSequence,
                            serverTimeSeconds));
                }
            }

            for (int index = 0; index < results.Count; index++)
            {
                chains.Remove(results[index].Killer);
            }
        }
    }

    internal void RemoveKiller(TKiller killer)
    {
        lock (sync)
        {
            chains.Remove(killer);
        }
    }

    internal void Reset()
    {
        lock (sync)
        {
            chains.Clear();
        }
    }

    private static KillChainTransition<TKiller> CreateActiveTransition(
        TKiller killer,
        int count,
        long serverSequence,
        double serverTimeSeconds,
        double expiresAt)
    {
        if (count < KillChainRules.BerserkerKillThreshold)
        {
            return new KillChainTransition<TKiller>(
                killer,
                KillChainTransitionKind.Progressed,
                KillChainTier.None,
                count,
                serverSequence,
                serverTimeSeconds,
                expiresAt);
        }

        if (count == KillChainRules.BerserkerKillThreshold)
        {
            return new KillChainTransition<TKiller>(
                killer,
                KillChainTransitionKind.Activated,
                KillChainTier.Berserker,
                count,
                serverSequence,
                serverTimeSeconds,
                expiresAt);
        }

        if (count < KillChainRules.SlaughterhouseKillThreshold)
        {
            return new KillChainTransition<TKiller>(
                killer,
                KillChainTransitionKind.Refreshed,
                KillChainTier.Berserker,
                count,
                serverSequence,
                serverTimeSeconds,
                expiresAt);
        }

        if (count == KillChainRules.SlaughterhouseKillThreshold)
        {
            return new KillChainTransition<TKiller>(
                killer,
                KillChainTransitionKind.Escalated,
                KillChainTier.Slaughterhouse,
                count,
                serverSequence,
                serverTimeSeconds,
                expiresAt);
        }

        return new KillChainTransition<TKiller>(
            killer,
            KillChainTransitionKind.Refreshed,
            KillChainTier.Slaughterhouse,
            count,
            serverSequence,
            serverTimeSeconds,
            expiresAt);
    }

    private static KillChainTransition<TKiller> CreateTerminalTransition(
        TKiller killer,
        long serverSequence,
        double serverTimeSeconds)
    {
        return new KillChainTransition<TKiller>(
            killer,
            KillChainTransitionKind.Expired,
            KillChainTier.None,
            killCount: 0,
            serverSequence,
            serverTimeSeconds,
            expiresAtServerTimeSeconds: 0d);
    }

    private static bool FiniteNonNegative(double value)
    {
        return !double.IsNaN(value)
            && !double.IsInfinity(value)
            && value >= 0d;
    }

    private readonly struct ActiveChain
    {
        internal ActiveChain(
            int killCount,
            long serverSequence,
            double expiresAtServerTimeSeconds)
        {
            KillCount = killCount;
            ServerSequence = serverSequence;
            ExpiresAtServerTimeSeconds = expiresAtServerTimeSeconds;
        }

        internal int KillCount { get; }
        internal long ServerSequence { get; }
        internal double ExpiresAtServerTimeSeconds { get; }
    }
}

internal readonly struct KillChainTransition<TKiller>
{
    internal KillChainTransition(
        TKiller killer,
        KillChainTransitionKind kind,
        KillChainTier tier,
        int killCount,
        long serverSequence,
        double serverTimeSeconds,
        double expiresAtServerTimeSeconds)
    {
        Killer = killer;
        Kind = kind;
        Tier = tier;
        KillCount = killCount;
        ServerSequence = serverSequence;
        ServerTimeSeconds = serverTimeSeconds;
        ExpiresAtServerTimeSeconds = expiresAtServerTimeSeconds;
    }

    internal TKiller Killer { get; }
    internal KillChainTransitionKind Kind { get; }
    internal KillChainTier Tier { get; }
    internal int KillCount { get; }
    internal long ServerSequence { get; }
    internal double ServerTimeSeconds { get; }
    internal double ExpiresAtServerTimeSeconds { get; }
}
