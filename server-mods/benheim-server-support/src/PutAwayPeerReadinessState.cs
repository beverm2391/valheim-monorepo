using System.Collections.Generic;

namespace BenheimServerSupport;

/// <summary>
/// Tracks the Put Away generation reported by each authenticated direct peer.
/// Unknown and mismatched peers block lease acquisition or continuation so the
/// requester cannot reserve items for a chest owner that lacks the deposit RPCs.
/// </summary>
internal sealed class PutAwayPeerReadinessState<TPeer> where TPeer : class
{
    private readonly object sync = new object();
    private readonly Dictionary<TPeer, int?> generations = new Dictionary<TPeer, int?>();
    private long revision;

    internal void Track(TPeer peer)
    {
        lock (sync)
        {
            if (!generations.ContainsKey(peer))
            {
                generations.Add(peer, null);
                revision++;
            }
        }
    }

    internal bool TryRecord(TPeer peer, int generation)
    {
        lock (sync)
        {
            if (!generations.ContainsKey(peer))
            {
                return false;
            }

            if (generations[peer] != generation)
            {
                generations[peer] = generation;
                revision++;
            }
            return true;
        }
    }

    internal void Remove(TPeer peer)
    {
        lock (sync)
        {
            if (generations.Remove(peer))
            {
                revision++;
            }
        }
    }

    internal bool AllConnectedPeersMatch(
        IEnumerable<TPeer> connectedPeers,
        int requiredGeneration,
        out string rejectionReason)
    {
        return AllConnectedPeersMatch(
            connectedPeers,
            requiredGeneration,
            out rejectionReason,
            out _);
    }

    internal bool AllConnectedPeersMatch(
        IEnumerable<TPeer> connectedPeers,
        int requiredGeneration,
        out string rejectionReason,
        out long cohortRevision)
    {
        lock (sync)
        {
            cohortRevision = revision;
            foreach (TPeer peer in connectedPeers)
            {
                if (!generations.TryGetValue(peer, out int? generation)
                    || !generation.HasValue)
                {
                    rejectionReason = "peer_protocol_unknown";
                    return false;
                }

                if (generation.Value != requiredGeneration)
                {
                    rejectionReason = "peer_protocol_incompatible";
                    return false;
                }
            }

            rejectionReason = string.Empty;
            return true;
        }
    }

    internal void Reset()
    {
        lock (sync)
        {
            if (generations.Count > 0)
            {
                generations.Clear();
                revision++;
            }
        }
    }
}
