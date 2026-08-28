using System;

namespace BenheimQoL.KillAttribution;

/// <summary>
/// Turns Valheim's silent disconnected-RPC no-op into an explicit failed
/// attempt. Callers can then reject or visibly degrade the operation instead
/// of claiming that an invocation was delivered.
/// </summary>
internal static class KillAttributionRpcAttempt
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
