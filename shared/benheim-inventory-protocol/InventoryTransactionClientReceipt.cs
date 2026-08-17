using System;
using System.Linq;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    private static void TrySendSettledReceiptAck(PendingDeposit pending)
    {
        SettledDeposit settled = pending.Settled!;
        if (!TrySendReceiptAck(pending, out string exceptionType))
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_receipt_ack_pending",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_id", pending.OperationId)
                    .Code("correlation", pending.TransactionId)
                    .Code("chest_id", StableChestId(pending.ContainerId))
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "pending")
                    .Code("reason", "send_failed")
                    .Code("exception_type", exceptionType)
                    .Integer("attempt", pending.Attempts)
                    .Integer("requested_count", CountReserved(pending.Items))
                    .Integer("accepted_count", settled.Accepted.Sum())
                    .Integer("refunded_count", settled.Refunded.Sum())
                    .Integer("dropped_count", settled.Dropped.Sum()));
            return;
        }

        Emit(
            InventoryTransactionDiagnosticEvent.Create("client_receipt_ack_sent", "requester")
                .Code("operation_id", pending.OperationId)
                .Code("correlation", pending.TransactionId)
                .Code("chest_id", StableChestId(pending.ContainerId))
                .Code("operation_phase", "receipt_ack")
                .Code("status", "awaiting_owner_ack")
                .Integer("attempt", pending.Attempts)
                .Integer("accepted_count", settled.Accepted.Sum())
                .Integer("refunded_count", settled.Refunded.Sum())
                .Integer("dropped_count", settled.Dropped.Sum()));
    }

    private static void RpcReceiptAckResult(long sender, ZPackage acknowledgement)
    {
        if (!IsExpectedServer(sender))
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_receipt_ack_rejected",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "rejected")
                    .Code("reason", "unexpected_sender"));
            return;
        }

        string transactionId;
        string payloadHash;
        try
        {
            transactionId = acknowledgement.ReadString();
            payloadHash = acknowledgement.ReadString();
        }
        catch (Exception exception)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_receipt_ack_rejected",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "rejected")
                    .Code("reason", "invalid_ack_result")
                    .Code("exception_type", exception.GetType().Name));
            return;
        }

        if (!ClientPending.TryGetValue(transactionId, out PendingDeposit? pending)
            || pending.PayloadHash != payloadHash
            || pending.Settled == null)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "client_receipt_ack_rejected",
                        "requester",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "rejected")
                    .Code("reason", "unknown_or_unsettled_correlation"));
            return;
        }

        SettledDeposit settled = pending.Settled;
        ClientPending.Remove(pending.TransactionId);
        Emit(
            InventoryTransactionDiagnosticEvent.Create("client_receipt_acknowledged", "requester")
                .Code("operation_id", pending.OperationId)
                .Code("correlation", pending.TransactionId)
                .Code("chest_id", StableChestId(pending.ContainerId))
                .Code("operation_phase", "receipt_ack")
                .Code("status", "acknowledged")
                .Integer("attempt", pending.Attempts));
        Emit(
            InventoryTransactionDiagnosticEvent.Create("client_result", "requester")
                .Code("operation_id", pending.OperationId)
                .Code("correlation", pending.TransactionId)
                .Code("chest_id", StableChestId(pending.ContainerId))
                .Code("operation_phase", "settled")
                .Code("status", "settled_receipt_acknowledged")
                .Code("reason", StatusCode(settled.Result.Status))
                .Integer("attempt", pending.Attempts)
                .Integer("revision_after", CurrentRevision(pending.ContainerId))
                .Integer("requested_count", CountReserved(pending.Items))
                .Integer("accepted_count", settled.Accepted.Sum())
                .Integer("refunded_count", settled.Refunded.Sum())
                .Integer("dropped_count", settled.Dropped.Sum())
                .Text("requested_items", DescribeReserved(pending.Items))
                .Text("accepted_items", DescribeAccepted(pending.Items, settled.Accepted))
                .Text("refunded_items", DescribeRefunded(pending.Items, settled.Refunded))
                .Text("dropped_items", DescribeAccepted(pending.Items, settled.Dropped))
                .Text("contents_after", DescribeLocalChest(pending.ContainerId)));
        pending.Callback(settled.Result);
    }

    private static bool TrySendReceiptAck(PendingDeposit pending, out string exceptionType)
    {
        exceptionType = string.Empty;
        try
        {
            ZPackage acknowledgement = new ZPackage();
            acknowledgement.Write(pending.TransactionId);
            acknowledgement.Write(pending.PayloadHash);
            acknowledgement.Write(pending.ContainerId);
            acknowledgement.Write(pending.RequestBytes);
            ZRoutedRpc.instance.InvokeRoutedRPC(ReceiptAckRpc, acknowledgement);
            return true;
        }
        catch (Exception exception)
        {
            exceptionType = exception.GetType().Name;
            return false;
        }
    }
}
