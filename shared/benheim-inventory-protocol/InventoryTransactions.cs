using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    internal const int ProtocolVersion = 1;
    internal const int MaxItemsPerDeposit = 64;
    internal const string HelloRpc = "Benheim.Inventory.v1.Hello";
    internal const string StatusRpc = "Benheim.Inventory.v1.Status";
    internal const string DepositRequestRpc = "Benheim.Inventory.v1.DepositRequest";
    internal const string OwnerExecuteRpc = "Benheim.Inventory.v1.OwnerExecute";
    internal const string OwnerResultRpc = "Benheim.Inventory.v1.OwnerResult";
    internal const string DepositResultRpc = "Benheim.Inventory.v1.DepositResult";
    internal const string ReceiptAckRpc = "Benheim.Inventory.v1.ReceiptAck";
    internal const string OwnerReceiptAckRpc = "Benheim.Inventory.v1.OwnerReceiptAck";

    private const float StatusStaleAfter = 6f;
    private static readonly Dictionary<long, int> PeerProtocols = new Dictionary<long, int>();
    private static readonly Dictionary<string, PendingDeposit> ClientPending = new Dictionary<string, PendingDeposit>();
    private static readonly Dictionary<string, PendingDeposit> ClientCompleted = new Dictionary<string, PendingDeposit>();
    private static readonly Dictionary<string, ServerDeposit> ServerPending = new Dictionary<string, ServerDeposit>();
    private static readonly Dictionary<string, CompletedServerDeposit> ServerCompleted = new Dictionary<string, CompletedServerDeposit>();
    private static ManualLogSource? log;
    private static ZRoutedRpc? registeredRpc;
    private static float lastStatusAt = float.NegativeInfinity;
    private static bool serverReady;
    private static bool clientReady;

    internal static void Initialize(ManualLogSource logger)
    {
        log = logger;
    }

    internal static void Shutdown()
    {
        registeredRpc = null;
        PeerProtocols.Clear();
        ClientPending.Clear();
        ClientCompleted.Clear();
        ServerPending.Clear();
        ServerCompleted.Clear();
        clientReady = false;
        serverReady = false;
    }

    internal static void Update()
    {
        if (ZNet.instance == null || ZRoutedRpc.instance == null)
        {
            return;
        }

        EnsureRegistered();
        RecoverPendingJournals();
        float now = Time.realtimeSinceStartup;
        if (ZNet.instance.IsServer())
        {
            UpdateServerCapabilities(now);
            ExpireServerResults(now);
        }
        else
        {
            UpdateClientHandshake(now);
        }

        RetryClientTransactions(now);
        ConfirmCompletedTransactions();
    }

    internal static bool IsAvailable(out string reason)
    {
        reason = string.Empty;
        if (ZNet.instance == null || ZRoutedRpc.instance == null)
        {
            reason = "Put Away is waiting for the world connection";
            return false;
        }

        if (ZNet.instance.IsServer())
        {
            if (ZNet.instance.GetConnectedPeers().Count == 0 || serverReady)
            {
                return true;
            }
        }
        else if (clientReady && Time.realtimeSinceStartup - lastStatusAt <= StatusStaleAfter)
        {
            return true;
        }

        reason = "Put Away needs matching Benheim versions on the server and every player";
        return false;
    }

    internal static long GetServerPeerId()
    {
        if (ZNet.instance == null || ZNet.instance.IsServer())
        {
            return ZNet.GetUID();
        }

        List<ZNetPeer> peers = ZNet.instance.GetConnectedPeers();
        return peers.Count > 0 ? peers[0].m_uid : 0L;
    }

    internal static bool IsExpectedServer(long sender)
    {
        return sender != 0L && sender == GetServerPeerId();
    }

    internal static void LogDiagnostic(string message)
    {
        log?.LogInfo($"[diag][InventoryTransaction] {message}");
    }

    internal static void LogWarning(string message)
    {
        log?.LogWarning($"[diag][InventoryTransaction] {message}");
    }
}
