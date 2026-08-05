using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    private const float HandshakeInterval = 2f;
    private const float ServerRosterPollInterval = 0.5f;
    private const int MaxCapabilityPlayers = 64;
    private const int MaxProductVersionLength = 64;
    private const int MaxPlayerNameLength = 128;
    private static float lastHelloAt = float.NegativeInfinity;
    private static float lastServerRosterPollAt = float.NegativeInfinity;
    private static float lastStatusBroadcastAt = float.NegativeInfinity;
    private static int lastConnected = -1;
    private static int lastCompatible = -1;
    private static bool lastClientReady;
    private static int lastClientConnected = -1;
    private static int lastClientCompatible = -1;
    private static int lastClientProtocol = -1;

    private static void EnsureRegistered()
    {
        if (ReferenceEquals(registeredRpc, ZRoutedRpc.instance))
        {
            return;
        }

        registeredRpc = ZRoutedRpc.instance;
        registeredRpc.Register<ZPackage>(HelloRpc, RpcHello);
        registeredRpc.Register<ZPackage>(StatusRpc, RpcStatus);
        registeredRpc.Register<ZPackage>(DepositRequestRpc, RpcDepositRequest);
        registeredRpc.Register<ZPackage>(OwnerExecuteRpc, InventoryTransactionOwner.Handle);
        registeredRpc.Register<ZPackage>(OwnerResultRpc, RpcOwnerResult);
        registeredRpc.Register<ZPackage>(DepositResultRpc, RpcDepositResult);
        registeredRpc.Register<ZPackage>(ReceiptAckRpc, RpcReceiptAck);
        registeredRpc.Register<ZPackage>(OwnerReceiptAckRpc, InventoryTransactionOwner.HandleReceiptAck);
        PeerCapabilities.Clear();
        ServerPending.Clear();
        ServerCompleted.Clear();
        clientReady = false;
        serverReady = false;
        lastHelloAt = float.NegativeInfinity;
        lastServerRosterPollAt = float.NegativeInfinity;
        lastStatusBroadcastAt = float.NegativeInfinity;
        lastStatusAt = float.NegativeInfinity;
        lastConnected = -1;
        lastCompatible = -1;
        lastClientReady = false;
        lastClientConnected = -1;
        lastClientCompatible = -1;
        lastClientProtocol = -1;
        capabilityStartedAt = Time.realtimeSinceStartup;
        capabilitySnapshot = ZNet.instance.IsServer()
            ? new InventoryCapabilitySnapshot(
                InventoryCapabilityState.Ready,
                localProductVersion,
                ProtocolVersion,
                Array.Empty<InventoryPeerCapability>())
            : InventoryCapabilitySnapshot.Disconnected;
        activeServerConnection = null;
        journalRecoveryAttempted = false;
        LogDiagnostic($"rpc_registered protocol={ProtocolVersion} server={ZNet.instance.IsServer()}");
    }

    private static void UpdateClientCapabilities(float now)
    {
        ZNetPeer? server = ZNet.instance.GetServerPeer();
        if (server == null || server.m_uid == 0L)
        {
            if (activeServerConnection != null)
            {
                ResetClientCapabilityState();
            }
            return;
        }

        if (!ReferenceEquals(server, activeServerConnection))
        {
            BeginClientCapabilityConnection(server, now);
        }

        UpdateClientHandshake(now);
    }

    private static void BeginClientCapabilityConnection(ZNetPeer server, float now)
    {
        activeServerConnection = server;
        clientReady = false;
        lastClientReady = false;
        lastClientConnected = -1;
        lastClientCompatible = -1;
        lastClientProtocol = -1;
        lastStatusAt = float.NegativeInfinity;
        lastHelloAt = float.NegativeInfinity;
        capabilityStartedAt = now;
        capabilitySnapshot = InventoryCapabilitySnapshot.Checking;
    }

    private static void ResetClientCapabilityState()
    {
        activeServerConnection = null;
        clientReady = false;
        lastClientReady = false;
        lastStatusAt = float.NegativeInfinity;
        capabilityStartedAt = float.NegativeInfinity;
        capabilitySnapshot = InventoryCapabilitySnapshot.Disconnected;
    }

    private static void UpdateClientHandshake(float now)
    {
        if (now - lastHelloAt < HandshakeInterval)
        {
            if (now - lastStatusAt > StatusStaleAfter && clientReady)
            {
                clientReady = false;
                lastClientReady = false;
                LogWarning("capability changed ready=false reason=status_stale");
            }
            return;
        }

        lastHelloAt = now;
        ZPackage hello = new ZPackage();
        hello.Write(ProtocolVersion);
        hello.Write(localProductVersion);
        ZRoutedRpc.instance.InvokeRoutedRPC(HelloRpc, hello);
    }

    private static void UpdateServerCapabilities(float now)
    {
        if (now - lastServerRosterPollAt < ServerRosterPollInterval)
        {
            return;
        }

        lastServerRosterPollAt = now;
        List<ZNetPeer> connected = ZNet.instance.GetConnectedPeers()
            .Where(peer => peer.IsReady())
            .OrderBy(peer => peer.m_playerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        PeerCapabilities.RemoveStaleConnections(connected);

        List<InventoryPeerCapability> players = connected.Select(peer =>
        {
            bool detected = PeerCapabilities.TryGet(
                peer.m_uid,
                peer,
                out InventoryPeerAdvertisement advertised);
            int protocol = advertised?.ProtocolVersion ?? 0;
            string version = advertised?.ClientVersion ?? string.Empty;
            return new InventoryPeerCapability(
                string.IsNullOrWhiteSpace(peer.m_playerName) ? "Player" : peer.m_playerName,
                version,
                protocol,
                detected,
                detected && protocol == ProtocolVersion);
        }).ToList();
        int compatible = players.Count(player => player.IsCompatible);
        bool ready = connected.Count == compatible;
        bool changed = ready != serverReady
            || connected.Count != lastConnected
            || compatible != lastCompatible;
        serverReady = ready;
        capabilitySnapshot = new InventoryCapabilitySnapshot(
            ready ? InventoryCapabilityState.Ready : InventoryCapabilityState.Incompatible,
            localProductVersion,
            ProtocolVersion,
            players);
        lastConnected = connected.Count;
        lastCompatible = compatible;
        if (!changed && now - lastStatusBroadcastAt < HandshakeInterval)
        {
            return;
        }

        lastStatusBroadcastAt = now;
        ZPackage status = new ZPackage();
        status.Write(ProtocolVersion);
        status.Write(localProductVersion);
        status.Write(ready);
        status.Write(connected.Count);
        status.Write(compatible);
        status.Write(players.Count);
        foreach (InventoryPeerCapability player in players)
        {
            status.Write(player.PlayerName);
            status.Write(player.ClientVersion);
            status.Write(player.ProtocolVersion);
            status.Write(player.IsDetected);
            status.Write(player.IsCompatible);
        }
        ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, StatusRpc, status);
        if (changed)
        {
            LogDiagnostic(
                $"capability changed ready={ready} connected={connected.Count} compatible={compatible}");
        }
    }

    private static bool ServerAllReadyPeersCompatible()
    {
        foreach (ZNetPeer peer in ZNet.instance.GetConnectedPeers())
        {
            if (peer.IsReady()
                && (!PeerCapabilities.TryGet(peer.m_uid, peer, out InventoryPeerAdvertisement advertised)
                    || advertised.ProtocolVersion != ProtocolVersion))
            {
                return false;
            }
        }

        return true;
    }

    private static void RpcHello(long sender, ZPackage package)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        try
        {
            int protocol = package.ReadInt();
            string version = package.ReadString();
            ZNetPeer? peer = ZNet.instance.GetPeer(sender);
            if (protocol <= 0
                || string.IsNullOrWhiteSpace(version)
                || version.Length > MaxProductVersionLength
                || peer == null)
            {
                throw new InvalidOperationException("capability hello failed validation");
            }

            PeerCapabilities.Record(sender, peer, protocol, version);
        }
        catch (Exception ex)
        {
            PeerCapabilities.Remove(sender);
            LogWarning($"hello_invalid sender={sender} error=\"{ex.Message}\"");
        }
    }

    private static void RpcStatus(long sender, ZPackage package)
    {
        if (ZNet.instance == null || ZNet.instance.IsServer() || !IsExpectedServer(sender))
        {
            return;
        }

        try
        {
            int protocol = package.ReadInt();
            string serverVersion = package.ReadString();
            bool advertisedReady = package.ReadBool();
            int connected = package.ReadInt();
            int compatible = package.ReadInt();
            int playerCount = package.ReadInt();
            if (protocol <= 0
                || string.IsNullOrWhiteSpace(serverVersion)
                || serverVersion.Length > MaxProductVersionLength
                || connected < 0
                || connected > MaxCapabilityPlayers
                || compatible < 0
                || compatible > connected
                || playerCount != connected)
            {
                throw new InvalidOperationException("capability status header failed validation");
            }

            List<InventoryPeerCapability> players = new List<InventoryPeerCapability>(playerCount);
            for (int index = 0; index < playerCount; index++)
            {
                string playerName = package.ReadString();
                string clientVersion = package.ReadString();
                int playerProtocol = package.ReadInt();
                bool isDetected = package.ReadBool();
                bool isCompatible = package.ReadBool();
                if (string.IsNullOrWhiteSpace(playerName)
                    || playerName.Length > MaxPlayerNameLength
                    || clientVersion.Length > MaxProductVersionLength
                    || isDetected != (playerProtocol > 0)
                    || (!isDetected && clientVersion.Length != 0)
                    || isCompatible != (isDetected && playerProtocol == protocol))
                {
                    throw new InvalidOperationException("capability player failed validation");
                }

                players.Add(new InventoryPeerCapability(
                    playerName,
                    clientVersion,
                    playerProtocol,
                    isDetected,
                    isCompatible));
            }
            if (compatible != players.Count(player => player.IsCompatible)
                || advertisedReady != (compatible == connected))
            {
                throw new InvalidOperationException("capability status totals failed validation");
            }

            bool nextReady = protocol == ProtocolVersion && advertisedReady;
            bool changed = nextReady != lastClientReady
                || connected != lastClientConnected
                || compatible != lastClientCompatible
                || protocol != lastClientProtocol;
            clientReady = nextReady;
            lastStatusAt = Time.realtimeSinceStartup;
            capabilitySnapshot = new InventoryCapabilitySnapshot(
                nextReady ? InventoryCapabilityState.Ready : InventoryCapabilityState.Incompatible,
                serverVersion,
                protocol,
                players);
            if (changed)
            {
                LogDiagnostic(
                    $"capability changed ready={clientReady} connected={connected} " +
                    $"compatible={compatible} protocol={protocol}");
                lastClientReady = clientReady;
                lastClientConnected = connected;
                lastClientCompatible = compatible;
                lastClientProtocol = protocol;
            }
        }
        catch (Exception ex)
        {
            clientReady = false;
            lastClientReady = false;
            lastStatusAt = Time.realtimeSinceStartup;
            capabilitySnapshot = InventoryCapabilitySnapshot.ServerMissing;
            LogWarning($"status_invalid error=\"{ex.Message}\"");
        }
    }
}
