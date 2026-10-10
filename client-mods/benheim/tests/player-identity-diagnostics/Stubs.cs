using System;
using System.Collections.Generic;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch() { }
        internal HarmonyPatch(Type type, string methodName) { }
    }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPrefix : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPostfix : Attribute { }
}

public struct PlatformUserID
{
    private readonly string? value;

    public PlatformUserID(string value, bool isValid)
    {
        this.value = value;
        IsValid = isValid;
    }

    public bool IsValid { get; }

    public override string ToString() => value ?? string.Empty;
}

public struct CrossNetworkUserInfo
{
    public PlatformUserID m_id;
}

public sealed class ZNetPeer
{
    public ZNetPeer(bool isServer) => m_server = isServer;
    public bool m_server;
}

public sealed class ZNet
{
    public struct PlayerInfo
    {
        public string m_name;
        public CrossNetworkUserInfo m_userInfo;
    }

    public static ZNet? instance;

    public ZNet(bool isServer) => Server = isServer;
    public bool Server { get; }
    public List<PlayerInfo> Players { get; } = new List<PlayerInfo>();

    public bool IsServer() => Server;
    public List<PlayerInfo> GetPlayerList() => Players;
}
