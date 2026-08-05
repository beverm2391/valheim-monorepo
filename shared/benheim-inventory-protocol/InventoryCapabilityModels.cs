using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BenheimInventoryProtocol;

internal enum InventoryCapabilityState
{
    Disconnected,
    Checking,
    Ready,
    Incompatible,
    ServerMissing,
}

internal sealed class InventoryPeerCapability
{
    internal InventoryPeerCapability(
        string playerName,
        string clientVersion,
        int protocolVersion,
        bool isDetected,
        bool isCompatible)
    {
        PlayerName = playerName;
        ClientVersion = clientVersion;
        ProtocolVersion = protocolVersion;
        IsDetected = isDetected;
        IsCompatible = isCompatible;
    }

    internal string PlayerName { get; }
    internal string ClientVersion { get; }
    internal int ProtocolVersion { get; }
    internal bool IsDetected { get; }
    internal bool IsCompatible { get; }
}

internal sealed class InventoryCapabilitySnapshot
{
    private string? displayFingerprint;
    private string? incompatibilityKey;
    private int incompatibilityKeyProtocol;
    internal static readonly InventoryCapabilitySnapshot Disconnected = new InventoryCapabilitySnapshot(
        InventoryCapabilityState.Disconnected,
        string.Empty,
        0,
        Array.Empty<InventoryPeerCapability>());
    internal static readonly InventoryCapabilitySnapshot Checking = new InventoryCapabilitySnapshot(
        InventoryCapabilityState.Checking,
        string.Empty,
        0,
        Array.Empty<InventoryPeerCapability>());
    internal static readonly InventoryCapabilitySnapshot ServerMissing = new InventoryCapabilitySnapshot(
        InventoryCapabilityState.ServerMissing,
        string.Empty,
        0,
        Array.Empty<InventoryPeerCapability>());

    internal InventoryCapabilitySnapshot(
        InventoryCapabilityState state,
        string serverVersion,
        int serverProtocol,
        IReadOnlyList<InventoryPeerCapability> players)
    {
        State = state;
        ServerVersion = serverVersion;
        ServerProtocol = serverProtocol;
        Players = players;
    }

    internal InventoryCapabilityState State { get; }
    internal string ServerVersion { get; }
    internal int ServerProtocol { get; }
    internal IReadOnlyList<InventoryPeerCapability> Players { get; }
    internal bool IsReady => State == InventoryCapabilityState.Ready;

    internal string GetDisplayFingerprint()
    {
        if (displayFingerprint != null)
        {
            return displayFingerprint;
        }

        StringBuilder fingerprint = new StringBuilder()
            .Append((int)State)
            .Append('|')
            .Append(ServerVersion)
            .Append('|')
            .Append(ServerProtocol);
        foreach (InventoryPeerCapability player in Players)
        {
            fingerprint
                .Append('|')
                .Append(player.PlayerName)
                .Append('|')
                .Append(player.ClientVersion)
                .Append('|')
                .Append(player.ProtocolVersion)
                .Append('|')
                .Append(player.IsDetected ? '1' : '0')
                .Append('|')
                .Append(player.IsCompatible ? '1' : '0');
        }

        displayFingerprint = fingerprint.ToString();
        return displayFingerprint;
    }

    // Semantic versions deliberately do not participate in this key. A warning
    // becomes eligible again only when the safety-relevant incompatible roster
    // changes, not when diagnostic version text changes.
    internal string GetIncompatibilityKey(int localProtocol)
    {
        if (State != InventoryCapabilityState.Incompatible
            && State != InventoryCapabilityState.ServerMissing)
        {
            return string.Empty;
        }
        if (incompatibilityKey != null && incompatibilityKeyProtocol == localProtocol)
        {
            return incompatibilityKey;
        }

        StringBuilder key = new StringBuilder()
            .Append((int)State)
            .Append("|server:")
            .Append(ServerProtocol)
            .Append(':')
            .Append(ServerProtocol == localProtocol ? '1' : '0');
        foreach (InventoryPeerCapability player in Players
            .Where(player => !player.IsCompatible)
            .OrderBy(player => player.PlayerName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(player => player.ProtocolVersion))
        {
            key
                .Append("|player:")
                .Append(player.PlayerName)
                .Append(':')
                .Append(player.ProtocolVersion)
                .Append(':')
                .Append(player.IsDetected ? '1' : '0');
        }

        incompatibilityKey = key.ToString();
        incompatibilityKeyProtocol = localProtocol;
        return incompatibilityKey;
    }
}

internal sealed class InventoryPeerAdvertisement
{
    internal InventoryPeerAdvertisement(object connection, int protocolVersion, string clientVersion)
    {
        Connection = connection;
        ProtocolVersion = protocolVersion;
        ClientVersion = clientVersion;
    }

    internal object Connection { get; }
    internal int ProtocolVersion { get; }
    internal string ClientVersion { get; }
}

internal sealed class InventoryPeerCapabilityRegistry
{
    private readonly Dictionary<long, InventoryPeerAdvertisement> advertisements = new();

    internal void Record(long peerId, object connection, int protocolVersion, string clientVersion)
    {
        advertisements[peerId] = new InventoryPeerAdvertisement(
            connection,
            protocolVersion,
            clientVersion);
    }

    internal bool TryGet(
        long peerId,
        object connection,
        out InventoryPeerAdvertisement advertisement)
    {
        if (advertisements.TryGetValue(peerId, out InventoryPeerAdvertisement? candidate)
            && ReferenceEquals(candidate.Connection, connection))
        {
            advertisement = candidate;
            return true;
        }

        advertisement = null!;
        return false;
    }

    internal void Remove(long peerId)
    {
        advertisements.Remove(peerId);
    }

    internal void RemoveStaleConnections<T>(IReadOnlyList<T> currentConnections) where T : class
    {
        List<long> stalePeerIds = new();
        foreach (KeyValuePair<long, InventoryPeerAdvertisement> pair in advertisements)
        {
            bool current = false;
            for (int index = 0; index < currentConnections.Count; index++)
            {
                if (ReferenceEquals(pair.Value.Connection, currentConnections[index]))
                {
                    current = true;
                    break;
                }
            }

            if (!current)
            {
                stalePeerIds.Add(pair.Key);
            }
        }

        foreach (long peerId in stalePeerIds)
        {
            advertisements.Remove(peerId);
        }
    }

    internal void Clear()
    {
        advertisements.Clear();
    }
}

internal sealed class InventoryCompatibilityWarningTracker
{
    private float candidateSince = float.NegativeInfinity;
    private string candidateKey = string.Empty;
    private string warnedKey = string.Empty;

    internal bool TryGetWarningKey(
        InventoryCapabilitySnapshot snapshot,
        int localProtocol,
        float now,
        float graceSeconds,
        out string warningKey)
    {
        warningKey = string.Empty;
        if (snapshot.State == InventoryCapabilityState.Disconnected)
        {
            candidateSince = float.NegativeInfinity;
            candidateKey = string.Empty;
            warnedKey = string.Empty;
            return false;
        }

        string nextKey = snapshot.GetIncompatibilityKey(localProtocol);
        if (nextKey.Length == 0)
        {
            candidateSince = float.NegativeInfinity;
            candidateKey = string.Empty;
            warnedKey = string.Empty;
            return false;
        }

        if (!string.Equals(nextKey, candidateKey, StringComparison.Ordinal))
        {
            candidateKey = nextKey;
            candidateSince = now;
            return false;
        }

        if (now - candidateSince < graceSeconds
            || string.Equals(nextKey, warnedKey, StringComparison.Ordinal))
        {
            return false;
        }

        warningKey = nextKey;
        return true;
    }

    internal void MarkWarned(string warningKey)
    {
        warnedKey = warningKey;
    }
}
