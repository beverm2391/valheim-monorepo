using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    internal const int ProtocolVersion = InventoryTransactionRecoveryPolicy.CurrentProtocolVersion;
    internal const int MaxItemsPerDeposit = 64;
    internal const string HelloRpc = "Benheim.Inventory.v2.Hello";
    internal const string StatusRpc = "Benheim.Inventory.v2.Status";
    internal const string DepositRequestRpc = "Benheim.Inventory.v2.DepositRequest";
    internal const string OwnerExecuteRpc = "Benheim.Inventory.v2.OwnerExecute";
    internal const string OwnerResultRpc = "Benheim.Inventory.v2.OwnerResult";
    internal const string DepositResultRpc = "Benheim.Inventory.v2.DepositResult";
    internal const string ReceiptAckRpc = "Benheim.Inventory.v2.ReceiptAck";
    internal const string OwnerReceiptAckRpc = "Benheim.Inventory.v2.OwnerReceiptAck";

    private const float StatusStaleAfter = 6f;
    private static readonly InventoryPeerCapabilityRegistry PeerCapabilities = new();
    private static readonly Dictionary<string, PendingDeposit> ClientPending = new Dictionary<string, PendingDeposit>();
    private static readonly Dictionary<string, PendingDeposit> ClientCompleted = new Dictionary<string, PendingDeposit>();
    private static readonly Dictionary<string, ServerDeposit> ServerPending = new Dictionary<string, ServerDeposit>();
    private static readonly Dictionary<string, CompletedServerDeposit> ServerCompleted = new Dictionary<string, CompletedServerDeposit>();
    private static ManualLogSource? log;
    private static ZRoutedRpc? registeredRpc;
    private static float lastStatusAt = float.NegativeInfinity;
    private static bool serverReady;
    private static bool clientReady;
    private static string localProductVersion = "unknown";
    private static float capabilityStartedAt = float.NegativeInfinity;
    private static InventoryCapabilitySnapshot capabilitySnapshot = InventoryCapabilitySnapshot.Disconnected;
    private static ZNetPeer? activeServerConnection;
    private static bool capabilityNetworkActive;

    internal static void Initialize(ManualLogSource logger, string productVersion)
    {
        log = logger;
        localProductVersion = string.IsNullOrWhiteSpace(productVersion) ? "unknown" : productVersion.Trim();
        if (InventoryTransactionAudit.Initialize(Paths.ConfigPath))
        {
            LogDiagnostic($"audit_session_start protocol={ProtocolVersion}");
        }
        else
        {
            log.LogWarning("[diag][InventoryTransaction] audit_disabled reason=initialize_failed");
        }
    }

    internal static void Shutdown()
    {
        if (ClientPending.Count > 0 || ClientCompleted.Count > 0 || ServerPending.Count > 0)
        {
            LogWarning(
                $"shutdown_pending client={ClientPending.Count} " +
                $"completed={ClientCompleted.Count} server={ServerPending.Count}");
        }
        LogDiagnostic("audit_session_end");
        registeredRpc = null;
        PeerCapabilities.Clear();
        ClientPending.Clear();
        ClientCompleted.Clear();
        ServerPending.Clear();
        ServerCompleted.Clear();
        clientReady = false;
        serverReady = false;
        capabilitySnapshot = InventoryCapabilitySnapshot.Disconnected;
        capabilityStartedAt = float.NegativeInfinity;
        activeServerConnection = null;
        capabilityNetworkActive = false;
    }

    internal static void Update()
    {
        if (ZNet.instance == null || ZRoutedRpc.instance == null)
        {
            ResetCapabilityConnection();
            return;
        }

        EnsureRegistered();
        capabilityNetworkActive = true;
        RecoverPendingJournals();
        float now = Time.realtimeSinceStartup;
        if (ZNet.instance.IsServer())
        {
            UpdateServerCapabilities(now);
            ExpireServerResults(now);
        }
        else
        {
            UpdateClientCapabilities(now);
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

        reason = "Put Away needs matching Benheim protocols on the server and every player";
        return false;
    }

    internal static InventoryCapabilitySnapshot GetCapabilitySnapshot()
    {
        if (ZNet.instance == null || ZRoutedRpc.instance == null)
        {
            return InventoryCapabilitySnapshot.Disconnected;
        }

        if (ZNet.instance.IsServer())
        {
            return capabilitySnapshot;
        }

        if (activeServerConnection == null)
        {
            return InventoryCapabilitySnapshot.Disconnected;
        }

        float now = Time.realtimeSinceStartup;
        if (lastStatusAt > float.NegativeInfinity && now - lastStatusAt <= StatusStaleAfter)
        {
            return capabilitySnapshot;
        }

        return now - capabilityStartedAt >= StatusStaleAfter
            ? InventoryCapabilitySnapshot.ServerMissing
            : InventoryCapabilitySnapshot.Checking;
    }

    internal static long GetServerPeerId()
    {
        if (ZNet.instance == null || ZNet.instance.IsServer())
        {
            return ZNet.GetUID();
        }

        return activeServerConnection?.m_uid ?? 0L;
    }

    internal static bool IsExpectedServer(long sender)
    {
        return activeServerConnection != null
            && sender != 0L
            && sender == activeServerConnection.m_uid;
    }

    internal static void LogDiagnostic(string message)
    {
        log?.LogInfo($"[diag][InventoryTransaction] {message}");
        InventoryTransactionAudit.Write("INFO", message);
    }

    internal static void LogWarning(string message)
    {
        log?.LogWarning($"[diag][InventoryTransaction] {message}");
        InventoryTransactionAudit.Write("WARN", message);
    }

    internal static string DescribeReserved(
        IReadOnlyList<ReservedDepositItem> items,
        IReadOnlyList<int>? accepted = null)
    {
        return string.Join(
            ",",
            items.Select((item, index) =>
            {
                string name = SafeItemName(item.Item);
                return accepted == null
                    ? $"{name}:{item.Item.m_stack}"
                    : $"{name}:{item.Item.m_stack}->{accepted[index]}";
            }));
    }

    internal static string DescribeRequested(
        IReadOnlyList<RequestedDepositItem> items,
        IReadOnlyList<int> accepted)
    {
        return string.Join(
            ",",
            items.Select((item, index) =>
                $"{SafeItemName(item.Item)}:{item.Item.m_stack}->{accepted[index]}"));
    }

    private static string SafeItemName(ItemDrop.ItemData item)
    {
        return item.m_shared.m_name.Replace(' ', '_').Replace('"', '\'');
    }

    private static void ResetCapabilityConnection()
    {
        if (!capabilityNetworkActive
            && activeServerConnection == null
            && capabilitySnapshot.State == InventoryCapabilityState.Disconnected)
        {
            return;
        }

        PeerCapabilities.Clear();
        clientReady = false;
        serverReady = false;
        activeServerConnection = null;
        capabilityStartedAt = float.NegativeInfinity;
        capabilitySnapshot = InventoryCapabilitySnapshot.Disconnected;
        lastStatusAt = float.NegativeInfinity;
        capabilityNetworkActive = false;
    }
}
