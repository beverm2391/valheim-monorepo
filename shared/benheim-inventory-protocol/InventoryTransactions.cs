using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;

namespace BenheimInventoryProtocol;

/// <summary>
/// Composition root for the correlated requester, server router, and current
/// chest-owner roles. See PROTOCOL.md before changing this message flow.
/// </summary>
internal static partial class InventoryTransactions
{
    internal const int ProtocolVersion = 3;
    internal const int MaxItemsPerDeposit = 64;
    internal const string DepositRequestRpc = "Benheim.Inventory.v3.DepositRequest";
    internal const string OwnerExecuteRpc = "Benheim.Inventory.v3.OwnerExecute";
    internal const string OwnerResultRpc = "Benheim.Inventory.v3.OwnerResult";
    internal const string DepositResultRpc = "Benheim.Inventory.v3.DepositResult";
    internal const string ReceiptAckRpc = "Benheim.Inventory.v3.ReceiptAck";
    internal const string OwnerReceiptAckRpc = "Benheim.Inventory.v3.OwnerReceiptAck";

    private static readonly Dictionary<string, PendingDeposit> ClientPending = new();
    private static readonly Dictionary<string, PendingDeposit> ClientCompleted = new();
    private static readonly ConnectedTransactionRouter<ZDOID> ServerRouter = new();
    private static ManualLogSource? log;
    private static ZRoutedRpc? registeredRpc;

    internal static void Initialize(ManualLogSource logger, string productVersion)
    { log = logger; LogDiagnostic($"initialized protocol={ProtocolVersion} product={Safe(productVersion)}"); }

    internal static void Shutdown()
    {
        if (ClientPending.Count > 0)
            LogWarning($"shutdown_reserved count={ClientPending.Count} recovery=unsupported action=reconnect_and_inspect_before_retry");
        registeredRpc = null;
        ClientPending.Clear(); ClientCompleted.Clear(); ServerRouter.Clear();
    }

    internal static void Update()
    {
        if (ZNet.instance == null || ZRoutedRpc.instance == null) return;
        EnsureRegistered();
        float now = Time.realtimeSinceStartup;
        RetryClientTransactions(now);
        ConfirmCompletedTransactions();
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
    internal static void LogDiagnostic(string message) => log?.LogInfo($"[diag][InventoryTransaction] {message}");
    internal static void LogWarning(string message) => log?.LogWarning($"[diag][InventoryTransaction] {message}");

    internal static string DescribeReserved(IReadOnlyList<ReservedDepositItem> items, IReadOnlyList<int>? accepted = null) =>
        string.Join(",", items.Select((item, index) => accepted == null
            ? $"{SafeItemName(item.Item)}:{item.Item.m_stack}"
            : $"{SafeItemName(item.Item)}:{item.Item.m_stack}->{accepted[index]}"));

    internal static string DescribeRequested(IReadOnlyList<RequestedDepositItem> items, IReadOnlyList<int> accepted) =>
        string.Join(",", items.Select((item, index) => $"{SafeItemName(item.Item)}:{item.Item.m_stack}->{accepted[index]}"));

    private static void EnsureRegistered()
    {
        if (ReferenceEquals(registeredRpc, ZRoutedRpc.instance)) return;
        registeredRpc = ZRoutedRpc.instance;
        registeredRpc.Register<ZPackage>(DepositRequestRpc, RpcDepositRequest);
        registeredRpc.Register<ZPackage>(OwnerExecuteRpc, InventoryTransactionOwner.Handle);
        registeredRpc.Register<ZPackage>(OwnerResultRpc, RpcOwnerResult);
        registeredRpc.Register<ZPackage>(DepositResultRpc, RpcDepositResult);
        registeredRpc.Register<ZPackage>(ReceiptAckRpc, RpcReceiptAck);
        registeredRpc.Register<ZPackage>(OwnerReceiptAckRpc, InventoryTransactionOwner.HandleReceiptAck);
        LogDiagnostic($"rpc_registered protocol={ProtocolVersion} server={ZNet.instance.IsServer()}");
    }

    private static string SafeItemName(ItemDrop.ItemData item) => Safe(item.m_shared.m_name);
    private static string Safe(string value) => (value ?? "unknown").Replace(' ', '_').Replace('"', '\'');
}
