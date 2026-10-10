using System.Collections.Generic;
using System.Linq;

namespace BenheimServerSupport;

/// <summary>
/// Tracks which connected peers have announced the resident RPC version. A
/// joining, leaving, or changing-version peer advances Revision so an active
/// speech request cannot cross a different compatibility cohort.
/// </summary>
internal sealed class ResidentPeerCohort<TPeer> where TPeer : class
{
    private readonly Dictionary<TPeer, int?> versions = new();

    internal long Revision { get; private set; }

    internal TPeer[] TrackedPeers => versions.Keys.ToArray();

    internal bool Track(TPeer peer)
    {
        if (versions.ContainsKey(peer))
        {
            return false;
        }

        versions.Add(peer, null);
        Revision++;
        return true;
    }

    internal bool TryRecordVersion(TPeer peer, int version, out int? priorVersion)
    {
        if (!versions.TryGetValue(peer, out priorVersion))
        {
            return false;
        }

        if (priorVersion != version)
        {
            versions[peer] = version;
            Revision++;
        }

        return true;
    }

    internal bool TryGetVersion(TPeer peer, out int? version) => versions.TryGetValue(peer, out version);

    internal bool Remove(TPeer peer)
    {
        if (!versions.Remove(peer))
        {
            return false;
        }

        Revision++;
        return true;
    }

    internal void Reset()
    {
        versions.Clear();
        Revision = 0L;
    }
}
