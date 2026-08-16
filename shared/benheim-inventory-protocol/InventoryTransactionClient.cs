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
        request.Write(ProtocolVersion); request.Write(transactionId);
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
            if (!source.RemoveItem(sourceItem, sourceItem.m_stack)) { Restore(source, removed); return false; }
            removed.Add(reserved[index]);
        }

        byte[] requestBytes = request.GetArray();
        string payloadHash = InventoryTransactionWire.Hash(requestBytes);
        PendingDeposit pending = new(operationId, transactionId, payloadHash, containerId, requestBytes, source, reserved, callback,
            Time.realtimeSinceStartup);
        ClientPending.Add(transactionId, pending);
        SendDepositRequest(pending);
        LogDiagnostic($"client_reserved_sent operation_id={operationId} tx={transactionId} chest={containerId} attempt=1 requested=\"{DescribeReserved(reserved)}\"");
        return true;
    }

    private static void RpcDepositResult(long sender, ZPackage response)
    {
        if (!IsExpectedServer(sender)
            || !InventoryTransactionWire.TryReadResponse(response, out string transactionId, out string payloadHash,
                out DepositStatus status, out List<int> accepted)
            || !ClientPending.TryGetValue(transactionId, out PendingDeposit? pending)
            || pending.PayloadHash != payloadHash) return;

        List<int> reservedCounts = pending.Items.Select(item => item.Item.m_stack).ToList();
        if (!InventoryTransactionSettlement.TryCreate(
                reservedCounts,
                accepted,
                out InventoryTransactionSettlement? settlement))
        {
            LogWarning($"client_result_rejected tx={transactionId} reason=item_count");
            return;
        }

        ClientPending.Remove(transactionId);
        List<DepositResultEntry> entries = new(pending.Items.Count);
        for (int index = 0; index < pending.Items.Count; index++)
        {
            ReservedDepositItem reserved = pending.Items[index];
            int acceptedAmount = settlement!.Accepted[index];
            RestoreRemainder(pending.SourceInventory, reserved, settlement.Rejected[index]);
            entries.Add(new DepositResultEntry(reserved.Item, acceptedAmount));
            accepted[index] = acceptedAmount;
        }
        ClientCompleted[transactionId] = pending;
        LogDiagnostic($"client_result operation_id={pending.OperationId} tx={transactionId} status={status} attempts={pending.Attempts} items=\"{DescribeReserved(pending.Items, accepted)}\"");
        pending.Callback(new DepositResult(status, entries));
    }

    private static void ConfirmCompletedTransactions()
    {
        if (ZNet.instance == null || ZNet.instance.IsServer() || ClientPending.Count != 0 || ClientCompleted.Count == 0
            || Game.instance == null || Player.m_localPlayer == null) return;
        try
        {
            Game.instance.SavePlayerProfile(setLogoutPoint: false);
            foreach (PendingDeposit completed in ClientCompleted.Values.ToList())
            { SendReceiptAck(completed); ClientCompleted.Remove(completed.TransactionId); LogDiagnostic($"client_saved_acknowledged tx={completed.TransactionId}"); }
        }
        catch (Exception exception) { LogWarning($"client_save_pending error=\"{exception.Message}\""); }
    }

    private static void RetryClientTransactions(float now)
    {
        foreach (PendingDeposit pending in ClientPending.Values.ToList())
        {
            if (now - pending.LastSentAt < RetryInterval) continue;
            pending.LastSentAt = now; pending.Attempts++; SendDepositRequest(pending);
            LogDiagnostic($"client_retry operation_id={pending.OperationId} tx={pending.TransactionId} chest={pending.ContainerId} attempt={pending.Attempts} age_seconds={now - pending.CreatedAt:0.0}");
        }
    }

    private static void SendDepositRequest(PendingDeposit pending) =>
        ZRoutedRpc.instance.InvokeRoutedRPC(DepositRequestRpc, new ZPackage(pending.RequestBytes));

    private static void SendReceiptAck(PendingDeposit pending)
    {
        ZPackage acknowledgement = new ZPackage();
        acknowledgement.Write(pending.TransactionId); acknowledgement.Write(pending.PayloadHash);
        acknowledgement.Write(pending.ContainerId); acknowledgement.Write(pending.RequestBytes);
        ZRoutedRpc.instance.InvokeRoutedRPC(ReceiptAckRpc, acknowledgement);
    }

    private static bool TryGetContainerId(Container container, out ZDOID id)
    {
        id = ZDOID.None; ZNetView? view = NetworkViewField.GetValue(container) as ZNetView;
        if (!view || !view.IsValid()) return false;
        id = view.GetZDO().m_uid; return !id.IsNone();
    }

    private static void Restore(Inventory inventory, IEnumerable<ReservedDepositItem> items)
    { foreach (ReservedDepositItem item in items) RestoreRemainder(inventory, item, item.Item.m_stack); }

    private static void RestoreRemainder(Inventory inventory, ReservedDepositItem reserved, int amount)
    {
        if (amount <= 0) return;
        ItemDrop.ItemData remainder = reserved.Item.Clone(); remainder.m_stack = amount;
        if (inventory.AddItem(remainder, reserved.SourcePosition) || inventory.AddItem(remainder)) return;
        Player? player = Player.m_localPlayer;
        if (player) ItemDrop.DropItem(remainder, remainder.m_stack,
            player!.transform.position + player.transform.forward + Vector3.up, player.transform.rotation);
        LogWarning($"client_refund_dropped item={remainder.m_shared.m_name} amount={remainder.m_stack}");
    }
}
