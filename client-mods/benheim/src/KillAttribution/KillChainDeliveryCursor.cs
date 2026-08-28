namespace BenheimQoL.KillAttribution;

/// <summary>
/// Rejects replayed or reordered server transition facts before they reach the
/// local Player Combat bus. Active transitions advance with confirmed-kill
/// order. An expiry deliberately carries the last kill's order, so it is
/// accepted exactly once only while that chain is locally open.
/// </summary>
internal sealed class KillChainDeliveryCursor
{
    private long lastServerSequence;
    private bool chainOpen;

    internal bool TryAccept(KillChainTransitionKind kind, long serverSequence)
    {
        if (kind == KillChainTransitionKind.Expired)
        {
            if (!chainOpen || serverSequence != lastServerSequence)
            {
                return false;
            }

            chainOpen = false;
            return true;
        }

        if (serverSequence <= lastServerSequence)
        {
            return false;
        }

        lastServerSequence = serverSequence;
        chainOpen = true;
        return true;
    }

    internal void Reset()
    {
        lastServerSequence = 0L;
        chainOpen = false;
    }
}
