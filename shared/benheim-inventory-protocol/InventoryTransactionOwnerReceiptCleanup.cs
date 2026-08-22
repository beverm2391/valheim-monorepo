namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactionOwner
{
    internal static void HandleReceiptAck(long sender, ZPackage acknowledgement)
    {
        if (!InventoryTransactions.IsExpectedServer(sender))
        {
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "owner_receipt_ack_rejected",
                        "chest_owner",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "rejected")
                    .Code("reason", "sender_not_server"));
            return;
        }

        if (!InventoryTransactionReceiptAcknowledgementCodec.TryRead(
                acknowledgement,
                out string transactionId,
                out string payloadHash,
                out ZDOID containerId))
        {
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "owner_receipt_ack_rejected",
                        "chest_owner",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "rejected")
                    .Code("reason", "malformed_ack"));
            return;
        }

        if (!TryResolveOwnedContainer(
                containerId,
                out Container? container,
                out ZDO? zdo))
        {
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "owner_receipt_ack_rejected",
                        "chest_owner",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "rejected")
                    .Code("reason", "not_current_owner"));
            return;
        }

        if (!InventoryTransactionReceipts.Remove(zdo!, transactionId, payloadHash))
        {
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "owner_receipt_ack_rejected",
                        "chest_owner",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "ignored")
                    .Code("reason", "receipt_not_found"));
            return;
        }

        InventoryTransactions.Emit(
            InventoryTransactionDiagnosticEvent.Create(
                    "owner_receipt_acknowledged",
                    "chest_owner")
                .Code("correlation", transactionId)
                .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                .Code("operation_phase", "receipt_ack")
                .Code("status", "acknowledged")
                .Integer("owner_peer", zdo!.GetOwner())
                .Integer("revision_after", zdo.DataRevision)
                .Text("contents_after", InventoryTransactions.DescribeInventory(container!.GetInventory())));
    }
}
