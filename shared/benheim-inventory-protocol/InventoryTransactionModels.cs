using System;
using System.Collections.Generic;

namespace BenheimInventoryProtocol;

internal enum DepositStatus
{
    Success = 0,
    Rejected = 1,
    ProtocolMismatch = 2,
    ServerNotReady = 3,
    AccessDenied = 4,
    InvalidRequest = 5,
    StaleOwner = 6,
    ChestMissing = 7,
    TransactionConflict = 8,
    ReceiptCapacity = 9,
}

internal sealed class DepositCandidate
{
    internal DepositCandidate(ItemDrop.ItemData sourceItem)
    {
        SourceItem = sourceItem;
    }

    internal ItemDrop.ItemData SourceItem { get; }
}

internal sealed class DepositResultEntry
{
    internal DepositResultEntry(ItemDrop.ItemData item, int accepted)
    {
        Item = item;
        Accepted = accepted;
    }

    internal ItemDrop.ItemData Item { get; }
    internal int Accepted { get; }
}

internal sealed class DepositResult
{
    internal DepositResult(DepositStatus status, List<DepositResultEntry> entries)
    {
        Status = status;
        Entries = entries;
    }

    internal DepositStatus Status { get; }
    internal List<DepositResultEntry> Entries { get; }
    internal bool Succeeded => Status == DepositStatus.Success;
}

internal sealed class ReservedDepositItem
{
    internal ReservedDepositItem(ItemDrop.ItemData item, Vector2i sourcePosition)
    {
        Item = item;
        SourcePosition = sourcePosition;
    }

    internal ItemDrop.ItemData Item { get; }
    internal Vector2i SourcePosition { get; }
}

internal sealed class RequestedDepositItem
{
    internal RequestedDepositItem(ItemDrop.ItemData item, Vector2i sourcePosition)
    {
        Item = item;
        SourcePosition = sourcePosition;
    }

    internal ItemDrop.ItemData Item { get; }
    internal Vector2i SourcePosition { get; }
}

internal sealed class PendingDeposit
{
    internal PendingDeposit(
        string transactionId,
        string payloadHash,
        ZDOID containerId,
        byte[] requestBytes,
        long playerId,
        long worldId,
        Inventory sourceInventory,
        List<ReservedDepositItem> items,
        Action<DepositResult> callback,
        float now)
    {
        TransactionId = transactionId;
        PayloadHash = payloadHash;
        ContainerId = containerId;
        RequestBytes = requestBytes;
        PlayerId = playerId;
        WorldId = worldId;
        SourceInventory = sourceInventory;
        Items = items;
        Callback = callback;
        LastSentAt = now;
    }

    internal string TransactionId { get; }
    internal string PayloadHash { get; }
    internal ZDOID ContainerId { get; }
    internal byte[] RequestBytes { get; }
    internal long PlayerId { get; }
    internal long WorldId { get; }
    internal Inventory SourceInventory { get; }
    internal List<ReservedDepositItem> Items { get; }
    internal Action<DepositResult> Callback { get; }
    internal float LastSentAt { get; set; }
    internal int Attempts { get; set; } = 1;
}

internal sealed class ServerDeposit
{
    internal ServerDeposit(long requester, string payloadHash, byte[] requestBytes, float now)
    {
        Requester = requester;
        PayloadHash = payloadHash;
        RequestBytes = requestBytes;
        CreatedAt = now;
    }

    internal long Requester { get; }
    internal HashSet<long> RoutedOwners { get; } = new HashSet<long>();
    internal string PayloadHash { get; }
    internal byte[] RequestBytes { get; }
    internal float CreatedAt { get; }
}

internal sealed class CompletedServerDeposit
{
    internal CompletedServerDeposit(long requester, string payloadHash, byte[] responseBytes, float completedAt)
    {
        Requester = requester;
        PayloadHash = payloadHash;
        ResponseBytes = responseBytes;
        CompletedAt = completedAt;
    }

    internal long Requester { get; }
    internal string PayloadHash { get; }
    internal byte[] ResponseBytes { get; }
    internal float CompletedAt { get; }
}
