using System;
using System.Collections.Generic;

namespace BepInEx
{
    public static class Paths
    {
        public static string ConfigPath { get; set; } = string.Empty;
    }
}

public readonly struct ZDOID : IEquatable<ZDOID>
{
    private readonly long userId;
    private readonly uint id;

    public ZDOID(long userId, uint id)
    {
        this.userId = userId;
        this.id = id;
    }

    public bool IsNone() => userId == 0 && id == 0;

    public bool Equals(ZDOID other) => userId == other.userId && id == other.id;

    public override bool Equals(object? value) => value is ZDOID other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(userId, id);

    public static bool operator ==(ZDOID left, ZDOID right) => left.Equals(right);

    public static bool operator !=(ZDOID left, ZDOID right) => !left.Equals(right);
}

public readonly struct PlatformUserID
{
    private readonly string value;

    public PlatformUserID(string value)
    {
        this.value = value;
    }

    public bool IsValid => !string.IsNullOrEmpty(value);

    public override string ToString() => value ?? string.Empty;
}

public struct CrossNetworkUserInfo
{
    public PlatformUserID m_id;
}

public sealed class ZNetPeer
{
    public long m_uid;
    public bool m_server;
    public ZDOID m_characterID;
    public string m_playerName = string.Empty;
}

public sealed class ZNet
{
    public struct PlayerInfo
    {
        public string m_name;
        public ZDOID m_characterID;
        public CrossNetworkUserInfo m_userInfo;
    }

    public static ZNet? instance;

    private readonly List<PlayerInfo> players;

    public ZNet(List<PlayerInfo> players)
    {
        this.players = players;
    }

    public bool IsServer() => true;

    public List<PlayerInfo> GetPlayerList() => players;
}
