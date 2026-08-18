using System;
using System.Collections.Generic;

namespace BenheimServerSupport;

/// <summary>
/// Retains failed chain deliveries in order for each killer without allowing a
/// disconnected client to grow server memory without bound. The network seam
/// owns retry timing; this type owns only bounded ordering and replacement of
/// the current retry record.
/// </summary>
internal sealed class KillChainDeliveryQueue<TKiller, TDelivery>
    where TKiller : notnull
{
    private readonly int maximumPerKiller;
    private readonly Dictionary<TKiller, LinkedList<TDelivery>> queues =
        new Dictionary<TKiller, LinkedList<TDelivery>>();
    private readonly List<TKiller> killers = new List<TKiller>();

    internal KillChainDeliveryQueue(int maximumPerKiller)
    {
        if (maximumPerKiller < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPerKiller));
        }

        this.maximumPerKiller = maximumPerKiller;
    }

    internal int KillerCount => killers.Count;

    internal TKiller KillerAt(int index) => killers[index];

    internal bool HasPending(TKiller killer)
    {
        return queues.TryGetValue(killer, out LinkedList<TDelivery>? queue)
            && queue.Count > 0;
    }

    internal bool Enqueue(TKiller killer, TDelivery delivery)
    {
        if (!queues.TryGetValue(killer, out LinkedList<TDelivery>? queue))
        {
            queue = new LinkedList<TDelivery>();
            queues.Add(killer, queue);
            killers.Add(killer);
        }

        if (queue.Count >= maximumPerKiller)
        {
            return false;
        }

        queue.AddLast(delivery);
        return true;
    }

    internal bool TryPeek(TKiller killer, out TDelivery delivery)
    {
        if (queues.TryGetValue(killer, out LinkedList<TDelivery>? queue)
            && queue.First != null)
        {
            delivery = queue.First.Value;
            return true;
        }

        delivery = default!;
        return false;
    }

    internal void ReplaceHead(TKiller killer, TDelivery delivery)
    {
        if (!queues.TryGetValue(killer, out LinkedList<TDelivery>? queue)
            || queue.First == null)
        {
            throw new InvalidOperationException("No pending delivery exists for this killer.");
        }

        queue.First.Value = delivery;
    }

    internal void MarkDelivered(TKiller killer)
    {
        if (!queues.TryGetValue(killer, out LinkedList<TDelivery>? queue)
            || queue.First == null)
        {
            throw new InvalidOperationException("No pending delivery exists for this killer.");
        }

        queue.RemoveFirst();
        if (queue.Count == 0)
        {
            queues.Remove(killer);
            killers.Remove(killer);
        }
    }

    internal void RemoveKiller(TKiller killer)
    {
        if (queues.Remove(killer))
        {
            killers.Remove(killer);
        }
    }

    internal void Reset()
    {
        queues.Clear();
        killers.Clear();
    }
}
