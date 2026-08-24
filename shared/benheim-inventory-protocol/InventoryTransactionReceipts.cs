using System.Collections.Generic;

namespace BenheimInventoryProtocol;

internal static class InventoryTransactionReceipts
{
    private static readonly int ReceiptKey = "com.benheim.inventory:deposit_receipts".GetStableHashCode();

    internal static bool TryRead(
        ZDO zdo,
        string transactionId,
        string payloadHash,
        out DepositStatus status,
        out List<int> accepted)
    {
        bool found = InventoryTransactionReceiptCodec.TryRead(
            zdo.GetString(ReceiptKey),
            transactionId,
            payloadHash,
            out bool conflict,
            out TransactionReceipt? receipt);
        if (!found)
        {
            status = DepositStatus.InvalidRequest;
            accepted = new List<int>();
            return false;
        }

        status = conflict ? DepositStatus.TransactionConflict : (DepositStatus)receipt!.Status;
        accepted = conflict ? new List<int>() : receipt!.Accepted;
        return true;
    }

    internal static void Record(
        ZDO zdo,
        string transactionId,
        string payloadHash,
        DepositStatus status,
        IReadOnlyList<int> accepted)
    {
        zdo.Set(
            ReceiptKey,
            InventoryTransactionReceiptCodec.Record(
                zdo.GetString(ReceiptKey),
                transactionId,
                payloadHash,
                (int)status,
                accepted));
    }

    internal static bool CanRecord(ZDO zdo, string transactionId)
    {
        return InventoryTransactionReceiptCodec.CanRecord(
            zdo.GetString(ReceiptKey),
            transactionId);
    }

    internal static bool Remove(ZDO zdo, string transactionId, string payloadHash)
    {
        string current = zdo.GetString(ReceiptKey);
        string updated = InventoryTransactionReceiptCodec.Remove(
            current,
            transactionId,
            payloadHash);
        if (updated == current)
        {
            return false;
        }

        zdo.Set(ReceiptKey, updated);
        return true;
    }
}
