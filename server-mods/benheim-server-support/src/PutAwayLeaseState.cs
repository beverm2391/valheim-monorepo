using System.Collections.Generic;

namespace BenheimServerSupport;

/// <summary>
/// A single in-memory lease. Equality is by the authenticated connection object
/// supplied by Valheim, not by an identity claimed in the request payload.
/// </summary>
internal sealed class PutAwayLeaseState<TPeer> where TPeer : class
{
    private readonly object sync = new object();
    private TPeer? owner;
    private string operationId = string.Empty;

    internal bool TryAcquire(TPeer peer, string requestedOperationId)
    {
        lock (sync)
        {
            if (owner != null)
            {
                return false;
            }

            owner = peer;
            operationId = requestedOperationId;
            return true;
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
    }
}
