using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    private const float HandshakeInterval = 2f;
    private static float lastHelloAt = float.NegativeInfinity;
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
        PeerProtocols.Clear();
        ServerPending.Clear();
        ServerCompleted.Clear();
        clientReady = false;
        serverReady = false;
        lastHelloAt = float.NegativeInfinity;
        lastStatusBroadcastAt = float.NegativeInfinity;
        lastStatusAt = float.NegativeInfinity;
        lastConnected = -1;
        lastCompatible = -1;
        lastClientReady = false;
        lastClientConnected = -1;
        lastClientCompatible = -1;
        lastClientProtocol = -1;
        journalRecoveryAttempted = false;
        LogDiagnostic($"rpc_registered protocol={ProtocolVersion} server={ZNet.instance.IsServer()}");
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
        ZRoutedRpc.instance.InvokeRoutedRPC(HelloRpc, hello);
    }

    private static void UpdateServerCapabilities(float now)
    {
        List<long> connected = ZNet.instance.GetConnectedPeers()
            .Where(peer => peer.IsReady())
            .Select(peer => peer.m_uid)
            .ToList();
        foreach (long stale in PeerProtocols.Keys.Where(uid => !connected.Contains(uid)).ToList())
        {
            PeerProtocols.Remove(stale);
        }

        int compatible = connected.Count(uid =>
            PeerProtocols.TryGetValue(uid, out int version) && version == ProtocolVersion);
        bool ready = connected.Count == compatible;
        bool changed = ready != serverReady
            || connected.Count != lastConnected
            || compatible != lastCompatible;
        serverReady = ready;
        lastConnected = connected.Count;
        lastCompatible = compatible;
        if (!changed && now - lastStatusBroadcastAt < HandshakeInterval)
        {
            return;
        }

        lastStatusBroadcastAt = now;
        ZPackage status = new ZPackage();
        status.Write(ProtocolVersion);
        status.Write(ready);
        status.Write(connected.Count);
        status.Write(compatible);
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
                && (!PeerProtocols.TryGetValue(peer.m_uid, out int version)
                    || version != ProtocolVersion))
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
            PeerProtocols[sender] = package.ReadInt();
        }
        catch (Exception ex)
        {
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
            int version = package.ReadInt();
            bool nextReady = version == ProtocolVersion && package.ReadBool();
            int connected = package.ReadInt();
            int compatible = package.ReadInt();
            bool changed = nextReady != lastClientReady
                || connected != lastClientConnected
                || compatible != lastClientCompatible
                || version != lastClientProtocol;
            clientReady = nextReady;
            lastStatusAt = Time.realtimeSinceStartup;
            if (changed)
            {
                LogDiagnostic(
                    $"capability changed ready={clientReady} connected={connected} " +
                    $"compatible={compatible} protocol={version}");
                lastClientReady = clientReady;
                lastClientConnected = connected;
                lastClientCompatible = compatible;
                lastClientProtocol = version;
            }
        }
        catch (Exception ex)
        {
            clientReady = false;
            lastClientReady = false;
            LogWarning($"status_invalid error=\"{ex.Message}\"");
        }
    }
}
