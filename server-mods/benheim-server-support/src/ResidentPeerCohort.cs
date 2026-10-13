using System;
using System.Collections.Generic;
using Benheim.Resident;
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
    private readonly Dictionary<TPeer, double> awaitingSupportSince = new();
    internal const double SupportAnnouncementGraceSeconds = 30d;

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
            awaitingSupportSince.Remove(peer);
            Revision++;
        }

        return true;
    }

    internal bool TryGetVersion(TPeer peer, out int? version) => versions.TryGetValue(peer, out version);

    // Start the support deadline only after native authentication. Password
    // entry may take arbitrarily long; it must not be called missing support.
    internal ResidentReadiness Evaluate(int expectedVersion, double now,
        Func<TPeer, bool> nativeReady, Func<TPeer, bool> connected)
    {
        ResidentReadiness result = ResidentReadiness.Ready;
        foreach (var pair in versions)
        {
            ResidentReadiness state;
            if (!nativeReady(pair.Key) || !connected(pair.Key))
            {
                awaitingSupportSince.Remove(pair.Key);
                state = ResidentReadiness.Connecting;
            }
            else if (pair.Value.HasValue)
            {
                state = pair.Value == expectedVersion ? ResidentReadiness.Ready : ResidentReadiness.Incompatible;
            }
            else
            {
                if (!awaitingSupportSince.TryGetValue(pair.Key, out double started))
                    awaitingSupportSince[pair.Key] = started = now;
                state = now - started >= SupportAnnouncementGraceSeconds
                    ? ResidentReadiness.MissingSupport : ResidentReadiness.AwaitingSupport;
            }
            // A confirmed bad peer dominates a different peer still joining.
            if ((int)state > (int)result) result = state;
        }
        return result;
    }

    internal bool Remove(TPeer peer)
    {
        if (!versions.Remove(peer))
        {
            return false;
        }

        awaitingSupportSince.Remove(peer);
        Revision++;
        return true;
    }

    internal void Reset()
    {
        versions.Clear();
        awaitingSupportSince.Clear();
        Revision = 0L;
    }
}
