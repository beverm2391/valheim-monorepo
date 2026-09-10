namespace BenheimTestCommands;

internal static class ServerAdminAuthorization
{
    internal static bool IsAdmin(ZRpc rpc)
    {
        if (ZNet.instance == null || rpc?.GetSocket() == null)
        {
            return false;
        }

        string host = rpc.GetSocket().GetHostName();
        if (string.IsNullOrEmpty(host))
        {
            return false;
        }

        if (ZNet.instance.IsAdmin(host))
        {
            return true;
        }

        // Valheim 1.0 can reject a raw numeric Steam socket identity even when
        // that exact value is present in the server's loaded admin list. Keep
        // the native parser first, then compare only the authenticated socket
        // identity against that same native list snapshot. This does not trust
        // a sender-carried ID or introduce another authorization source.
        var adminList = ZNet.instance.GetAdminList();
        if (adminList == null)
        {
            return false;
        }

        return adminList.Contains(host) ||
            (IsDecimalIdentity(host) && adminList.Contains("Steam_" + host));
    }

    internal static bool IsDecimalIdentity(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (char character in value)
        {
            if (character < '0' || character > '9')
            {
                return false;
            }
        }

        return true;
    }
}
