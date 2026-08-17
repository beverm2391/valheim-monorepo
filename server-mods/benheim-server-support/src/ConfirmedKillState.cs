using System;
using System.Collections.Generic;

namespace BenheimServerSupport;

/// <summary>
/// Owns only transport-level kill identity: duplicate suppression and a
/// monotonic per-killer order. Chain windows, tiers, and rewards deliberately
/// remain outside this state until product tuning selects them.
/// </summary>
internal sealed class ConfirmedKillState<TVictim, TKiller>
    where TVictim : notnull
    where TKiller : notnull
{
    private readonly object sync = new object();
    private readonly int victimCapacity;
    private readonly HashSet<TVictim> confirmedVictims = new HashSet<TVictim>();
    private readonly Queue<TVictim> victimOrder = new Queue<TVictim>();
    private readonly Dictionary<TKiller, long> killerSequences = new Dictionary<TKiller, long>();

    internal ConfirmedKillState(int victimCapacity)
    {
        if (victimCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(victimCapacity));
        }

        this.victimCapacity = victimCapacity;
    }

    internal bool TryConfirm(TVictim victim, TKiller killer, out long sequence)
    {
        lock (sync)
        {
            if (confirmedVictims.Contains(victim))
            {
                sequence = 0L;
                return false;
            }

            while (victimOrder.Count >= victimCapacity)
            {
                confirmedVictims.Remove(victimOrder.Dequeue());
            }

            confirmedVictims.Add(victim);
            victimOrder.Enqueue(victim);
            killerSequences.TryGetValue(killer, out long previous);
            sequence = checked(previous + 1L);
            killerSequences[killer] = sequence;
            return true;
        }
    }

    internal void RemoveKiller(TKiller killer)
    {
        lock (sync)
        {
            killerSequences.Remove(killer);
        }
    }

    /// <summary>
    /// Releases only duplicate suppression when the confirmation could not be
    /// handed to the transport. A later replay may then deliver the kill. The
    /// already-issued sequence remains consumed, so delivered confirmations
    /// stay monotonically ordered even when the transport leaves a gap.
    /// </summary>
    internal void ReleaseFailedDelivery(TVictim victim)
    {
        lock (sync)
        {
            if (!confirmedVictims.Remove(victim))
            {
                return;
            }

            Queue<TVictim> retained = new Queue<TVictim>(victimOrder.Count);
            while (victimOrder.Count > 0)
            {
                TVictim current = victimOrder.Dequeue();
                if (!EqualityComparer<TVictim>.Default.Equals(current, victim))
                {
                    retained.Enqueue(current);
                }
            }

            while (retained.Count > 0)
            {
                victimOrder.Enqueue(retained.Dequeue());
            }
        }
    }

    internal void Reset()
    {
        lock (sync)
        {
            confirmedVictims.Clear();
            victimOrder.Clear();
            killerSequences.Clear();
        }
    }
}
