using System;
using System.Collections.Generic;

namespace BenheimInventoryProtocol;

internal enum DepositStatus { Success, Rejected, AccessDenied, InvalidRequest, StaleOwner, TransactionConflict, ReceiptCapacity }

internal sealed class DepositCandidate
{
    internal DepositCandidate(ItemDrop.ItemData sourceItem) => SourceItem = sourceItem;
    internal ItemDrop.ItemData SourceItem { get; }
}

internal sealed class DepositResultEntry
{
    internal DepositResultEntry(ItemDrop.ItemData item, int accepted) { Item = item; Accepted = accepted; }
    internal ItemDrop.ItemData Item { get; }
    internal int Accepted { get; }
}

internal sealed class DepositResult
{
    internal DepositResult(DepositStatus status, List<DepositResultEntry> entries) { Status = status; Entries = entries; }
    internal DepositStatus Status { get; }
    internal List<DepositResultEntry> Entries { get; }
    internal bool Succeeded => Status == DepositStatus.Success;
}

internal sealed class ReservedDepositItem
{
    internal ReservedDepositItem(ItemDrop.ItemData item, Vector2i sourcePosition) { Item = item; SourcePosition = sourcePosition; }
    internal ItemDrop.ItemData Item { get; }
    internal Vector2i SourcePosition { get; }
}

internal sealed class RequestedDepositItem
{
    internal RequestedDepositItem(ItemDrop.ItemData item, Vector2i sourcePosition) { Item = item; SourcePosition = sourcePosition; }
    internal ItemDrop.ItemData Item { get; }
    internal Vector2i SourcePosition { get; }
}

internal sealed class PendingDeposit
{
    internal PendingDeposit(string operationId, string transactionId, string payloadHash, ZDOID containerId, byte[] requestBytes,
        Inventory sourceInventory, List<ReservedDepositItem> items, Action<DepositResult> callback, float now)
    {
        OperationId = operationId; TransactionId = transactionId; PayloadHash = payloadHash; ContainerId = containerId; RequestBytes = requestBytes;
        SourceInventory = sourceInventory; Items = items; Callback = callback; CreatedAt = now; LastSentAt = now;
    }
    internal string OperationId { get; }
    internal string TransactionId { get; }
    internal string PayloadHash { get; }
    internal ZDOID ContainerId { get; }
    internal byte[] RequestBytes { get; }
    internal Inventory SourceInventory { get; }
    internal List<ReservedDepositItem> Items { get; }
    internal Action<DepositResult> Callback { get; }
    internal float CreatedAt { get; }
    internal float LastSentAt { get; set; }
    internal int Attempts { get; set; } = 1;
}
