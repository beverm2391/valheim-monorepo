using BenheimTestCommands;
using System;
using System.Collections.Generic;

internal static class Program
{
    private static void Main()
    {
        AssertAuthorized("native result remains authoritative", "socket-host", true, Array.Empty<string>());
        AssertAuthorized("exact raw socket host is accepted", "76561198000000000", false, new[] { "76561198000000000" });
        AssertAuthorized("numeric socket host accepts explicit Steam entry", "76561198000000000", false, new[] { "Steam_76561198000000000" });
        AssertRejected("nonnumeric host does not gain a Steam fallback", "external-user", new[] { "Steam_external-user" });
        AssertRejected("unrelated admin remains rejected", "76561198000000000", new[] { "76561198999999999" });
        AssertRejected("empty socket host remains rejected", string.Empty, new[] { string.Empty, "Steam_" });
        Console.WriteLine("server admin authorization policy checks passed");
    }

    private static void AssertAuthorized(string name, string socketHost, bool nativeResult, IEnumerable<string> admins)
    {
        ZNet.instance = new ZNet(nativeResult, admins);
        if (!ServerAdminAuthorization.IsAdmin(new ZRpc(socketHost)))
        {
            throw new InvalidOperationException(name);
        }
    }

    private static void AssertRejected(string name, string socketHost, IEnumerable<string> admins)
    {
        ZNet.instance = new ZNet(false, admins);
        if (ServerAdminAuthorization.IsAdmin(new ZRpc(socketHost)))
        {
            throw new InvalidOperationException(name);
        }
    }
}

internal sealed class ZRpc
{
    private readonly ZSocket socket;

    internal ZRpc(string host)
    {
        socket = new ZSocket(host);
    }

    internal ZSocket GetSocket() => socket;
}

internal sealed class ZSocket
{
    private readonly string host;

    internal ZSocket(string value)
    {
        host = value;
    }

    internal string GetHostName() => host;
}

internal sealed class ZNet
{
    internal static ZNet instance;
    private readonly bool nativeResult;
    private readonly List<string> admins;

    internal ZNet(bool native, IEnumerable<string> entries)
    {
        nativeResult = native;
        admins = new List<string>(entries);
    }

    internal bool IsAdmin(string host) => nativeResult;
    internal List<string> GetAdminList() => admins;
}
