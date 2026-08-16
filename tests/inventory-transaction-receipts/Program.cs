using System;
using System.Collections.Generic;
using BenheimInventoryProtocol;

string ledger = InventoryTransactionReceiptCodec.Record(
    string.Empty,
    "tx-a",
    "hash-a",
    status: 0,
    new[] { 2, 5 });

Assert(
    InventoryTransactionReceiptCodec.TryRead(
        ledger,
        "tx-a",
        "hash-a",
        out bool conflict,
        out TransactionReceipt? receipt),
    "recorded transaction was not found");
Assert(!conflict, "matching payload was marked as a conflict");
Assert(receipt != null && receipt.Status == 0, "status did not round-trip");
Assert(receipt != null && string.Join(",", receipt.Accepted) == "2,5", "accepted amounts did not round-trip");

Assert(
    InventoryTransactionReceiptCodec.TryRead(
        ledger,
        "tx-a",
        "different-hash",
        out conflict,
        out receipt),
    "transaction ID reuse was not detected");
Assert(conflict, "transaction ID reuse did not produce a conflict");

ledger = InventoryTransactionReceiptCodec.Record(
    ledger,
    "tx-a",
    "hash-a",
    status: 1,
    new[] { 0, 0 });
Assert(ledger.Split(';').Length == 1, "re-recording a transaction created a duplicate receipt");
Assert(
    InventoryTransactionReceiptCodec.TryRead(
        ledger,
        "tx-a",
        "hash-a",
        out conflict,
        out receipt)
        && receipt != null
        && receipt.Status == 1,
    "re-recorded result did not replace the prior receipt");

for (int index = 0; index < InventoryTransactionReceiptCodec.MaxReceipts - 1; index++)
{
    ledger = InventoryTransactionReceiptCodec.Record(
        ledger,
        $"tx-{index}",
        $"hash-{index}",
        status: 0,
        new[] { index });
}

Assert(
    !InventoryTransactionReceiptCodec.CanRecord(ledger, "tx-new"),
    "full receipt ledger accepted a new transaction");
AssertThrows(
    () => InventoryTransactionReceiptCodec.Record(
        ledger,
        "tx-new",
        "hash-new",
        status: 0,
        new[] { 1 }),
    "Record bypassed the full receipt ledger guard");
Assert(
    InventoryTransactionReceiptCodec.TryRead(
        ledger,
        "tx-0",
        "hash-0",
        out conflict,
        out receipt),
    "old receipt was evicted before acknowledgement");
Assert(
    InventoryTransactionReceiptCodec.TryRead(
        ledger,
        $"tx-{InventoryTransactionReceiptCodec.MaxReceipts - 2}",
        $"hash-{InventoryTransactionReceiptCodec.MaxReceipts - 2}",
        out conflict,
        out receipt)
        && !conflict
        && receipt != null
        && receipt.Accepted[0] == InventoryTransactionReceiptCodec.MaxReceipts - 2,
    "newest receipt was not retained");

ledger = InventoryTransactionReceiptCodec.Remove(ledger, "tx-a", "hash-a");
Assert(
    InventoryTransactionReceiptCodec.CanRecord(ledger, "tx-new"),
    "acknowledgement did not free receipt capacity");

Console.WriteLine("inventory transaction receipt tests passed");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows(Action action, string message)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
