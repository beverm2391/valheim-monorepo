using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BenheimInventoryProtocol;

/// <summary>
/// Composition root for the correlated requester, server router, and current
/// chest-owner roles. See PROTOCOL.md before changing this message flow.
/// </summary>
internal static partial class InventoryTransactions
{
    internal const int MaxItemsPerDeposit = 64;

    private static readonly Dictionary<string, PendingDeposit> ClientPending = new();
    private static readonly ConnectedTransactionRouter<ZDOID> ServerRouter = new();
    private static IInventoryTransactionDiagnosticSink? diagnosticSink;
    private static ZRoutedRpc? registeredRpc;

    internal static void Initialize(
        IInventoryTransactionDiagnosticSink sink,
        string productVersion)
    {
        diagnosticSink = sink;
        Emit(
            InventoryTransactionDiagnosticEvent.Create("initialized", HostRole())
                .Integer("protocol_version", InventoryTransactionProtocol.Version)
                .Code("product_version", productVersion));
    }

    internal static void Shutdown()
    {
        if (ClientPending.Count > 0)
        {
            foreach (PendingDeposit pending in ClientPending.Values)
            {
                Emit(
                    InventoryTransactionDiagnosticEvent.Create(
                            "shutdown_reserved",
                            "requester",
                            InventoryTransactionDiagnosticLevel.Warning)
                        .Code("operation_id", pending.OperationId)
                        .Code("correlation", pending.TransactionId)
                        .Code("operation_phase", "unsupported_shutdown")
                        .Code("status", "reservation_pending")
                        .Code("reason", "reconnect_recovery_unsupported")
                        .Integer("pending_count", ClientPending.Count));
            }
        }
        registeredRpc = null;
        ClientPending.Clear(); ServerRouter.Clear();
        diagnosticSink = null;
    }

    internal static void Update()
    {
        if (ZNet.instance == null || ZRoutedRpc.instance == null) return;
        EnsureRegistered();
        float now = Time.realtimeSinceStartup;
        RetryClientTransactions(now);
        if (ZNet.instance.IsServer()) ExpireServerResults(now);
    }

    internal static bool IsAvailable(out string reason)
    {
        reason = string.Empty;
        if (ZNet.instance == null || ZRoutedRpc.instance == null)
        { reason = "Put Away is waiting for the world connection"; return false; }
        if (!ZNet.instance.IsServer() && ZNet.instance.GetServerPeer() == null)
        { reason = "Put Away cannot reach Benheim Server Support"; return false; }
        EnsureRegistered();
        return true;
    }

    internal static long GetServerPeerId() => ZNet.instance == null || ZNet.instance.IsServer()
        ? ZNet.GetUID()
        : ZNet.instance.GetServerPeer()?.m_uid ?? 0L;

    internal static bool IsExpectedServer(long sender) => sender != 0L && sender == GetServerPeerId();
    internal static void Emit(InventoryTransactionDiagnosticEvent diagnosticEvent) =>
        InventoryTransactionDiagnosticProjection.EmitBestEffort(diagnosticSink, diagnosticEvent);

    internal static int CountReserved(IReadOnlyList<ReservedDepositItem> items) =>
        items.Sum(item => item.Item.m_stack);

    internal static int CountRequested(IReadOnlyList<RequestedDepositItem> items) =>
        items.Sum(item => item.Item.m_stack);

    internal static int CountAccepted(IReadOnlyList<int> accepted) => accepted.Sum();

    internal static bool HasUnsettledClientDeposit =>
        ClientPending.Count > 0;

    internal static void RemoveServerRequester(long requester)
    {
        if (requester == 0L)
        {
            return;
        }

        int removedCount = ServerRouter.RemoveRequester(requester);
        Emit(
            InventoryTransactionDiagnosticEvent.Create("server_requester_disconnected", "server_router")
                .Code("operation_phase", "disconnect_cleanup")
                .Code("status", "removed")
                .Integer("requester_peer", requester)
                .Integer("removed_count", removedCount));
    }

    internal static string DescribeReserved(IReadOnlyList<ReservedDepositItem> items) =>
        DescribeItemCounts(items.Select(item => (ItemName(item.Item), item.Item.m_stack)));

    internal static string DescribeRequested(IReadOnlyList<RequestedDepositItem> items) =>
        DescribeItemCounts(items.Select(item => (ItemName(item.Item), item.Item.m_stack)));

    internal static string DescribeAccepted(
        IReadOnlyList<RequestedDepositItem> items,
        IReadOnlyList<int> accepted) =>
        DescribeItemCounts(items.Select((item, index) =>
            (ItemName(item.Item), index < accepted.Count ? accepted[index] : 0)));

    internal static string DescribeAccepted(
        IReadOnlyList<ReservedDepositItem> items,
        IReadOnlyList<int> accepted) =>
        DescribeItemCounts(items.Select((item, index) =>
            (ItemName(item.Item), index < accepted.Count ? accepted[index] : 0)));

    internal static string DescribeResultEntries(IReadOnlyList<DepositResultEntry> entries) =>
        DescribeItemCounts(entries.Select(entry =>
            (ItemName(entry.Item), entry.Accepted)));

    internal static string DescribeSingleItem(ItemDrop.ItemData item, int count) =>
        DescribeItemCounts(new[] { (ItemName(item), count) });

    internal static string DescribeRefunded(
        IReadOnlyList<ReservedDepositItem> items,
        IReadOnlyList<int> rejected) =>
        DescribeItemCounts(items.Select((item, index) =>
            (ItemName(item.Item), index < rejected.Count ? rejected[index] : 0)));

    internal static string DescribeInventory(Inventory inventory) =>
        DescribeItemCounts(inventory.GetAllItems().Select(item =>
            (ItemName(item), item.m_stack)));

    internal static string StableChestId(ZDOID containerId) => containerId.ToString();

    private static string DescribeItemCounts(IEnumerable<(string Name, int Count)> items)
    {
        SortedDictionary<string, int> totals = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach ((string name, int count) in items)
        {
            totals.TryGetValue(name, out int total);
            totals[name] = total + count;
        }

        return string.Join(",", totals.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string ItemName(ItemDrop.ItemData item) =>
        item.m_dropPrefab != null && !string.IsNullOrEmpty(item.m_dropPrefab.name)
            ? item.m_dropPrefab.name
            : item.m_shared.m_name;

    internal static void BatchStarted(string operationId)
    {
        Emit(
            InventoryTransactionDiagnosticEvent.Create("put_away_batch_started", "requester")
                .Code("operation_id", operationId)
                .Code("operation_phase", "start")
                .Code("status", "running"));
    }

    internal static void BatchFinished(
        string operationId,
        string status,
        string reason,
        int acceptedCount,
        double batchDurationMs,
        double scanMatchDurationMs)
    {
        Emit(
            InventoryTransactionDiagnosticEvent.Create("put_away_batch_finished", "requester")
                .Code("operation_id", operationId)
                .Code("operation_phase", "terminal")
                .Code("status", status)
                .Code("reason", reason)
                .Integer("accepted_count", acceptedCount)
                .Number("batch_duration_ms", batchDurationMs)
                .Number("scan_match_duration_ms", scanMatchDurationMs));
    }

    private static void EnsureRegistered()
    {
        if (ReferenceEquals(registeredRpc, ZRoutedRpc.instance)) return;
        registeredRpc = ZRoutedRpc.instance;
        registeredRpc.Register<ZPackage>(
            InventoryTransactionProtocol.DepositRequestRpc,
            RpcDepositRequest);
        registeredRpc.Register<ZPackage>(
            InventoryTransactionProtocol.OwnerExecuteRpc,
            InventoryTransactionOwner.Handle);
        registeredRpc.Register<ZPackage>(
            InventoryTransactionProtocol.OwnerResultRpc,
            RpcOwnerResult);
        registeredRpc.Register<ZPackage>(
            InventoryTransactionProtocol.DepositResultRpc,
            RpcDepositResult);
        registeredRpc.Register<ZPackage>(
            InventoryTransactionProtocol.ReceiptAckRpc,
            RpcReceiptAck);
        registeredRpc.Register<ZPackage>(
            InventoryTransactionProtocol.OwnerReceiptAckRpc,
            InventoryTransactionOwner.HandleReceiptAck);
        Emit(
            InventoryTransactionDiagnosticEvent.Create("rpc_registered", HostRole())
                .Integer("protocol_version", InventoryTransactionProtocol.Version)
                .Boolean("server", ZNet.instance.IsServer()));
    }

    private static string HostRole() =>
        ZNet.instance != null && ZNet.instance.IsServer()
            ? "server_router"
            : "client_peer";
}
