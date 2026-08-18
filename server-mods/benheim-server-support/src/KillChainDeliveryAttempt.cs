using System;

namespace BenheimServerSupport;

/// <summary>
/// Turns Valheim's silent disconnected-RPC no-op into an explicit failed
/// attempt so the ordered outbox retains the transition for retry.
/// </summary>
internal static class KillChainDeliveryAttempt
{
    internal static bool TrySend(bool isConnected, Action send, out string failure)
    {
        if (!isConnected)
        {
            failure = "rpc_disconnected";
            return false;
        }

        try
        {
            send();
            failure = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            failure = $"delivery_failed_{exception.GetType().Name}";
            return false;
        }
    }
}
