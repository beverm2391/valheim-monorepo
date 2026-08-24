using System.Collections.Generic;

namespace BenheimServerSupport;

/// <summary>
/// A single in-memory lease. Equality is by the authenticated connection object
/// supplied by Valheim, not by an identity claimed in the request payload. The
/// acquisition cohort must remain unchanged for later reservation validation.
/// </summary>
internal sealed class PutAwayLeaseState<TPeer> where TPeer : class
{
    private readonly object sync = new object();
    private TPeer? owner;
    private string operationId = string.Empty;
    private long cohortRevision;

    internal bool TryAcquire(TPeer peer, string requestedOperationId)
    {
        return TryAcquireOrValidate(peer, requestedOperationId, 0L)
            == PutAwayLeaseRequestDecision.Acquired;
    }

    internal PutAwayLeaseRequestDecision TryAcquireOrValidate(
        TPeer peer,
        string requestedOperationId,
        long currentCohortRevision)
    {
        lock (sync)
        {
            if (owner == null)
            {
                owner = peer;
                operationId = requestedOperationId;
                cohortRevision = currentCohortRevision;
                return PutAwayLeaseRequestDecision.Acquired;
            }

            if (!EqualityComparer<TPeer>.Default.Equals(owner, peer)
                || operationId != requestedOperationId)
            {
                return PutAwayLeaseRequestDecision.Busy;
            }

            return cohortRevision == currentCohortRevision
                ? PutAwayLeaseRequestDecision.Validated
                : PutAwayLeaseRequestDecision.CohortChanged;
        }
    }

    internal bool TryRelease(TPeer peer, string requestedOperationId)
    {
        lock (sync)
        {
            if (owner == null
                || !EqualityComparer<TPeer>.Default.Equals(owner, peer)
                || operationId != requestedOperationId)
            {
                return false;
            }

            Clear();
            return true;
        }
    }

    internal bool TryReleasePeer(TPeer peer, out string releasedOperationId)
    {
        lock (sync)
        {
            releasedOperationId = string.Empty;
            if (owner == null || !EqualityComparer<TPeer>.Default.Equals(owner, peer))
            {
                return false;
            }

            releasedOperationId = operationId;
            Clear();
            return true;
        }
    }

    internal void Reset()
    {
        lock (sync)
        {
            Clear();
        }
    }

    private void Clear()
    {
        owner = null;
        operationId = string.Empty;
        cohortRevision = 0L;
    }
}

internal enum PutAwayLeaseRequestDecision
{
    Acquired,
    Validated,
    CohortChanged,
    Busy,
}
