using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    private const float RetryInterval = 1.5f;
    private static readonly FieldInfo NetworkViewField = AccessTools.Field(typeof(Container), "m_nview");

    internal static bool TryBeginDeposit(string operationId, Player player, Container container, IReadOnlyList<DepositCandidate> candidates,
        Action<DepositResult> callback)
    {
        if (!IsAvailable(out _) || candidates.Count == 0 || candidates.Count > MaxItemsPerDeposit
            || !TryGetContainerId(container, out ZDOID containerId)) return false;

        Inventory source = player.GetInventory();
        string transactionId = Guid.NewGuid().ToString("N");
        ZPackage request = new ZPackage();
        request.Write(InventoryTransactionProtocol.Version); request.Write(transactionId);
        request.Write(Game.instance.GetPlayerProfile().GetPlayerID()); request.Write(containerId); request.Write(candidates.Count);
        List<ItemDrop.ItemData> sourceItems = new();
        List<ReservedDepositItem> reserved = new();
        foreach (DepositCandidate candidate in candidates)
        {
            ItemDrop.ItemData sourceItem = candidate.SourceItem;
            if (sourceItem == null || sourceItem.m_stack <= 0 || !source.ContainsItem(sourceItem)) return false;
            ItemDrop.ItemData clone = sourceItem.Clone();
            Vector2i sourcePosition = sourceItem.m_gridPos;
            request.Write(sourcePosition); InventoryTransactionWire.WriteItem(request, clone);
            sourceItems.Add(sourceItem); reserved.Add(new ReservedDepositItem(clone, sourcePosition));
        }

        List<ReservedDepositItem> removed = new();
        for (int index = 0; index < sourceItems.Count; index++)
        {
            ItemDrop.ItemData sourceItem = sourceItems[index];
            if (!source.RemoveItem(sourceItem, sourceItem.m_stack))
            {
                List<int> dropped = Restore(
                    source,
                    removed,
                    operationId,
                    transactionId,
                    containerId,
                    player);
                List<int> refunded = removed.Select((item, removedIndex) =>
                    item.Item.m_stack - dropped[removedIndex]).ToList();
                Emit(
                    InventoryTransactionDiagnosticEvent.Create(
                            "client_reservation_rejected",
                            "requester",
                            InventoryTransactionDiagnosticLevel.Warning)
                        .Code("operation_id", operationId)
                        .Code("correlation", transactionId)
                        .Code("chest_id", StableChestId(containerId))
                        .Code("operation_phase", "reservation")
                        .Code("status", "rejected")
                        .Code("reason", "source_remove_failed")
                        .Integer("requested_count", CountReserved(reserved))
                        .Integer("refunded_count", refunded.Sum())
                        .Integer("dropped_count", dropped.Sum())
                        .Text("requested_items", DescribeReserved(reserved))
                        .Text("refunded_items", DescribeRefunded(removed, refunded))
                        .Text("dropped_items", DescribeAccepted(removed, dropped)));
                return false;
            }
            removed.Add(reserved[index]);
        }

        byte[] requestBytes = request.GetArray();
        string payloadHash = InventoryTransactionWire.Hash(requestBytes);
        PendingDeposit pending = new(operationId, transactionId, payloadHash, containerId, requestBytes, source, reserved, callback,
            Time.realtimeSinceStartup);
        ClientPending.Add(transactionId, pending);
        SendDepositRequest(pending);
        Emit(
            InventoryTransactionDiagnosticEvent.Create("client_reservation_sent", "requester")
                .Code("operation_id", operationId)
                .Code("correlation", transactionId)
                .Code("chest_id", StableChestId(containerId))
                .Code("operation_phase", "start")
                .Code("status", "sent")
                .Integer("attempt", 1)
                .Integer("revision_before", CurrentRevision(containerId))
                .Integer("requested_count", CountReserved(reserved))
                .Number("chest_position_x", container.transform.position.x)
                .Number("chest_position_y", container.transform.position.y)
                .Number("chest_position_z", container.transform.position.z)
                .Text("requested_items", DescribeReserved(reserved))
                .Text("contents_before", DescribeInventory(container.GetInventory())));
        return true;
    }

    private static void RpcDepositResult(long sender, ZPackage response)
    {
        if (!IsExpectedServer(sender))
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_result_rejected",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "result")
                    .Code("status", "rejected")
                    .Code("reason", "unexpected_sender"));
            return;
        }

        if (!InventoryTransactionWire.TryReadResponse(
                response,
                out string transactionId,
                out string payloadHash,
                out DepositStatus status,
                out List<int> accepted))
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_result_rejected",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "result")
                    .Code("status", "rejected")
                    .Code("reason", "invalid_response"));
            return;
        }

        if (!ClientPending.TryGetValue(transactionId, out PendingDeposit? pending))
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_result_rejected",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("operation_phase", "result")
                    .Code("status", "rejected")
                    .Code("reason", "unknown_correlation"));
            return;
        }

        if (pending.PayloadHash != payloadHash)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_result_rejected",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_id", pending.OperationId)
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(pending.ContainerId))
                    .Code("operation_phase", "result")
                    .Code("status", "rejected")
                    .Code("reason", "payload_hash_mismatch"));
            return;
        }

        List<int> reservedCounts = pending.Items.Select(item => item.Item.m_stack).ToList();
        if (!InventoryTransactionSettlement.TryCreate(
                reservedCounts,
                accepted,
                out InventoryTransactionSettlement? settlement))
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_result_rejected",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_id", pending.OperationId)
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(pending.ContainerId))
                    .Code("operation_phase", "result")
                    .Code("status", "rejected")
                    .Code("reason", "accepted_count_mismatch")
                    .Integer("requested_count", CountReserved(pending.Items)));
            return;
        }

        Player? localPlayer = Player.m_localPlayer;
        if (!InventoryTransactionLifecyclePolicy.CanSettle(localPlayer))
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_result_deferred",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_id", pending.OperationId)
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(pending.ContainerId))
                    .Code("operation_phase", "owner_result")
                    .Code("status", "pending")
                    .Code("reason", "local_player_unavailable")
                    .Integer("attempt", pending.Attempts)
                    .Integer("requested_count", CountReserved(pending.Items))
                    .Integer("accepted_count", settlement!.Accepted.Sum())
                    .Integer("refunded_count", settlement.Rejected.Sum())
                    .Text("requested_items", DescribeReserved(pending.Items)));
            return;
        }

        InventoryTransactionSettlement completedSettlement = settlement!;
        List<DepositResultEntry> entries = new(pending.Items.Count);
        List<int> refunded = completedSettlement.Rejected.ToList();
        List<int> dropped = Enumerable.Repeat(0, pending.Items.Count).ToList();
        for (int index = 0; index < pending.Items.Count; index++)
        {
            ReservedDepositItem reserved = pending.Items[index];
            int acceptedAmount = completedSettlement.Accepted[index];
            InventoryTransactionRefundPlacement placement = RestoreRemainder(
                pending.SourceInventory,
                reserved,
                completedSettlement.Rejected[index],
                pending.OperationId,
                transactionId,
                pending.ContainerId,
                localPlayer!);
            if (placement == InventoryTransactionRefundPlacement.WorldDrop)
            {
                dropped[index] = completedSettlement.Rejected[index];
                refunded[index] = 0;
            }
            entries.Add(new DepositResultEntry(reserved.Item, acceptedAmount));
            accepted[index] = acceptedAmount;
        }
        DepositResult result = new DepositResult(status, entries);
        ClientPending.Remove(transactionId);
        try
        {
            pending.Callback(result);
        }
        finally
        {
            TrySendReceiptAcknowledgement(pending);
            EmitSettledResult(pending, result, completedSettlement.Accepted, refunded, dropped);
        }
    }

    private static void EmitSettledResult(
        PendingDeposit pending,
        DepositResult result,
        IReadOnlyList<int> accepted,
        IReadOnlyList<int> refunded,
        IReadOnlyList<int> dropped)
    {
        Emit(
            InventoryTransactionDiagnosticEvent.Create("client_result", "requester")
                .Code("operation_id", pending.OperationId)
                .Code("correlation", pending.TransactionId)
                .Code("chest_id", StableChestId(pending.ContainerId))
                .Code("operation_phase", "settled")
                .Code("status", "settled")
                .Code("reason", StatusCode(result.Status))
                .Integer("attempt", pending.Attempts)
                .Integer("revision_after", CurrentRevision(pending.ContainerId))
                .Integer("requested_count", CountReserved(pending.Items))
                .Integer("accepted_count", accepted.Sum())
                .Integer("refunded_count", refunded.Sum())
                .Integer("dropped_count", dropped.Sum())
                .Text("requested_items", DescribeReserved(pending.Items))
                .Text("accepted_items", DescribeAccepted(pending.Items, accepted))
                .Text("refunded_items", DescribeRefunded(pending.Items, refunded))
                .Text("dropped_items", DescribeAccepted(pending.Items, dropped))
                .Text("contents_after", DescribeLocalChest(pending.ContainerId)));
    }

    private static void TrySendReceiptAcknowledgement(PendingDeposit pending)
    {
        try
        {
            ZPackage acknowledgement = InventoryTransactionReceiptAcknowledgementCodec.Write(
                pending.TransactionId,
                pending.PayloadHash,
                pending.ContainerId);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                InventoryTransactionProtocol.ReceiptAckRpc,
                acknowledgement);
            Emit(
                InventoryTransactionDiagnosticEvent.Create("client_receipt_ack_sent", "requester")
                    .Code("operation_id", pending.OperationId)
                    .Code("correlation", pending.TransactionId)
                    .Code("chest_id", StableChestId(pending.ContainerId))
                    .Code("operation_phase", "receipt_cleanup")
                    .Code("status", "sent"));
        }
        catch (Exception exception)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_receipt_ack_failed",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_id", pending.OperationId)
                    .Code("correlation", pending.TransactionId)
                    .Code("chest_id", StableChestId(pending.ContainerId))
                    .Code("operation_phase", "receipt_cleanup")
                    .Code("status", "failed")
                    .Code("reason", "send_failed")
                    .Code("exception_type", exception.GetType().Name));
        }
    }

    private static void RetryClientTransactions(float now)
    {
        foreach (PendingDeposit pending in ClientPending.Values.ToList())
        {
            if (now - pending.LastSentAt < RetryInterval) continue;
            pending.LastSentAt = now;
            pending.Attempts++;
            SendDepositRequest(pending);
            Emit(
                InventoryTransactionDiagnosticEvent.Create("client_retry", "requester")
                    .Code("operation_id", pending.OperationId)
                    .Code("correlation", pending.TransactionId)
                    .Code("chest_id", StableChestId(pending.ContainerId))
                    .Code("operation_phase", "retry")
                    .Code("status", "sent")
                    .Integer("attempt", pending.Attempts)
                    .Integer("requested_count", CountReserved(pending.Items))
                    .Text("requested_items", DescribeReserved(pending.Items))
                    .Number("age_seconds", now - pending.CreatedAt));
        }
    }

    private static void SendDepositRequest(PendingDeposit pending) =>
        ZRoutedRpc.instance.InvokeRoutedRPC(
            InventoryTransactionProtocol.DepositRequestRpc,
            new ZPackage(pending.RequestBytes));

    private static bool TryGetContainerId(Container container, out ZDOID id)
    {
        id = ZDOID.None; ZNetView? view = NetworkViewField.GetValue(container) as ZNetView;
        if (!view || !view.IsValid()) return false;
        id = view.GetZDO().m_uid; return !id.IsNone();
    }

    private static long CurrentRevision(ZDOID containerId) =>
        ZDOMan.instance?.GetZDO(containerId)?.DataRevision ?? 0U;

    private static string DescribeLocalChest(ZDOID containerId)
    {
        ZDO? zdo = ZDOMan.instance?.GetZDO(containerId);
        ZNetView? view = zdo != null ? ZNetScene.instance?.FindInstance(zdo) : null;
        Container? container = view ? view.GetComponentInChildren<Container>() : null;
        return container ? DescribeInventory(container.GetInventory()) : "unavailable";
    }

    private static List<int> Restore(
        Inventory inventory,
        IEnumerable<ReservedDepositItem> items,
        string operationId,
        string transactionId,
        ZDOID containerId,
        Player player)
    {
        List<int> dropped = new List<int>();
        foreach (ReservedDepositItem item in items)
        {
            InventoryTransactionRefundPlacement placement = RestoreRemainder(
                inventory,
                item,
                item.Item.m_stack,
                operationId,
                transactionId,
                containerId,
                player);
            dropped.Add(placement == InventoryTransactionRefundPlacement.WorldDrop
                ? item.Item.m_stack
                : 0);
        }

        return dropped;
    }

    private static InventoryTransactionRefundPlacement RestoreRemainder(
        Inventory inventory,
        ReservedDepositItem reserved,
        int amount,
        string operationId,
        string transactionId,
        ZDOID containerId,
        Player player)
    {
        if (amount <= 0) return InventoryTransactionRefundPlacement.OriginalSlot;
        ItemDrop.ItemData remainder = reserved.Item.Clone(); remainder.m_stack = amount;
        bool restoredToOriginalSlot = inventory.AddItem(remainder, reserved.SourcePosition);
        bool restoredElsewhere = !restoredToOriginalSlot && inventory.AddItem(remainder);
        InventoryTransactionRefundPlacement placement = InventoryTransactionRefundPolicy.Decide(
            restoredToOriginalSlot,
            restoredElsewhere);
        if (placement != InventoryTransactionRefundPlacement.WorldDrop)
        {
            return placement;
        }

        ItemDrop.DropItem(remainder, remainder.m_stack,
            player.transform.position + player.transform.forward + Vector3.up, player.transform.rotation);
        Emit(
            InventoryTransactionDiagnosticEvent.Create(
                    "client_refund_dropped",
                    "requester",
                    InventoryTransactionDiagnosticLevel.Warning)
                .Code("operation_id", operationId)
                .Code("correlation", transactionId)
                .Code("chest_id", StableChestId(containerId))
                .Code("operation_phase", "refund")
                .Code("status", "dropped")
                .Code("reason", "inventory_full")
                .Integer("dropped_count", remainder.m_stack)
                .Text("dropped_items", DescribeSingleItem(remainder, remainder.m_stack)));
        return placement;
    }

    private static string StatusCode(DepositStatus status) =>
        status.ToString().ToLowerInvariant();
}
