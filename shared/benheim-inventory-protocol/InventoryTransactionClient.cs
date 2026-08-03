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
    private static bool journalRecoveryAttempted;

    internal static bool TryBeginDeposit(
        Player player,
        Container container,
        IReadOnlyList<DepositCandidate> candidates,
        Action<DepositResult> callback)
    {
        if (!IsAvailable(out _)
            || candidates.Count == 0
            || candidates.Count > MaxItemsPerDeposit
            || !TryGetContainerId(container, out ZDOID containerId))
        {
            return false;
        }

        Inventory source = player.GetInventory();
        List<ReservedDepositItem> reserved = new List<ReservedDepositItem>();
        string transactionId = Guid.NewGuid().ToString("N");
        long playerId = Game.instance.GetPlayerProfile().GetPlayerID();
        long worldId = ZNet.instance.GetWorldUID();
        ZPackage request = new ZPackage();
        request.Write(ProtocolVersion);
        request.Write(transactionId);
        request.Write(playerId);
        request.Write(containerId);
        request.Write(candidates.Count);
        List<ItemDrop.ItemData> sourceItems = new List<ItemDrop.ItemData>();
        foreach (DepositCandidate candidate in candidates)
        {
            ItemDrop.ItemData sourceItem = candidate.SourceItem;
            if (sourceItem == null || sourceItem.m_stack <= 0 || !source.ContainsItem(sourceItem))
            {
                return false;
            }

            ItemDrop.ItemData clone = sourceItem.Clone();
            Vector2i sourcePosition = sourceItem.m_gridPos;
            request.Write(sourcePosition);
            InventoryTransactionWire.WriteItem(request, clone);
            sourceItems.Add(sourceItem);
            reserved.Add(new ReservedDepositItem(clone, sourcePosition));
        }

        byte[] requestBytes = request.GetArray();
        string payloadHash = InventoryTransactionWire.Hash(requestBytes);
        try
        {
            InventoryTransactionJournal.WritePrepared(
                playerId,
                worldId,
                transactionId,
                payloadHash,
                containerId,
                requestBytes);
        }
        catch (Exception ex)
        {
            LogWarning($"journal_prepare_failed tx={transactionId} error=\"{ex.Message}\"");
            return false;
        }

        List<ReservedDepositItem> removed = new List<ReservedDepositItem>();
        for (int index = 0; index < sourceItems.Count; index++)
        {
            ItemDrop.ItemData sourceItem = sourceItems[index];
            if (!source.RemoveItem(sourceItem, sourceItem.m_stack))
            {
                Restore(source, removed);
                InventoryTransactionJournal.Delete(transactionId, playerId, worldId);
                return false;
            }

            removed.Add(reserved[index]);
        }

        PendingDeposit pending = new PendingDeposit(
            transactionId,
            payloadHash,
            containerId,
            requestBytes,
            playerId,
            worldId,
            source,
            reserved,
            callback,
            Time.realtimeSinceStartup);
        try
        {
            InventoryTransactionJournal.MarkReserved(pending);
        }
        catch (Exception ex)
        {
            Restore(source, reserved);
            ClientCompleted[transactionId] = pending;
            LogWarning($"journal_reserve_failed tx={transactionId} error=\"{ex.Message}\"");
            return false;
        }

        ClientPending.Add(transactionId, pending);
        SendDepositRequest(pending);
        LogDiagnostic($"client_sent tx={transactionId} chest={containerId} items={reserved.Count} attempt=1");
        return true;
    }

    private static void RpcDepositResult(long sender, ZPackage response)
    {
        if (!IsExpectedServer(sender)
            || !InventoryTransactionWire.TryReadResponse(
                response,
                out string transactionId,
                out string payloadHash,
                out DepositStatus status,
                out List<int> accepted)
            || !ClientPending.TryGetValue(transactionId, out PendingDeposit? pending)
            || pending.PayloadHash != payloadHash)
        {
            return;
        }

        if (accepted.Count != pending.Items.Count)
        {
            if (status == DepositStatus.Success)
            {
                LogWarning($"client_result_invalid tx={transactionId} reason=item_count");
                return;
            }

            accepted = Enumerable.Repeat(0, pending.Items.Count).ToList();
        }

        try
        {
            InventoryTransactionJournal.MarkCompleted(pending, accepted);
        }
        catch (Exception ex)
        {
            LogWarning($"journal_complete_failed tx={transactionId} error=\"{ex.Message}\"");
            return;
        }

        ClientPending.Remove(transactionId);
        List<DepositResultEntry> entries = new List<DepositResultEntry>(pending.Items.Count);
        for (int index = 0; index < pending.Items.Count; index++)
        {
            ReservedDepositItem reserved = pending.Items[index];
            int acceptedAmount = Mathf.Clamp(accepted[index], 0, reserved.Item.m_stack);
            RestoreRemainder(pending.SourceInventory, reserved, reserved.Item.m_stack - acceptedAmount);
            entries.Add(new DepositResultEntry(reserved.Item, acceptedAmount));
        }

        ClientCompleted[transactionId] = pending;
        LogDiagnostic(
            $"client_result tx={transactionId} status={status} accepted={string.Join(",", accepted)} attempts={pending.Attempts}");
        pending.Callback(new DepositResult(status, entries));
    }

    private static void SendReceiptAck(PendingDeposit pending)
    {
        ZPackage acknowledgement = new ZPackage();
        acknowledgement.Write(pending.TransactionId);
        acknowledgement.Write(pending.PayloadHash);
        acknowledgement.Write(pending.ContainerId);
        ZRoutedRpc.instance.InvokeRoutedRPC(ReceiptAckRpc, acknowledgement);
    }

    private static void ConfirmCompletedTransactions()
    {
        if (ZNet.instance == null
            || ZNet.instance.IsServer()
            || ClientPending.Count != 0
            || ClientCompleted.Count == 0
            || Game.instance == null
            || Player.m_localPlayer == null)
        {
            return;
        }

        try
        {
            Game.instance.SavePlayerProfile(setLogoutPoint: false);
            foreach (PendingDeposit completed in ClientCompleted.Values.ToList())
            {
                InventoryTransactionJournal.Delete(completed);
                SendReceiptAck(completed);
                ClientCompleted.Remove(completed.TransactionId);
                LogDiagnostic($"client_committed tx={completed.TransactionId}");
            }
        }
        catch (Exception ex)
        {
            LogWarning($"client_commit_failed error=\"{ex.Message}\"");
        }
    }

    private static void RetryClientTransactions(float now)
    {
        foreach (PendingDeposit pending in ClientPending.Values.ToList())
        {
            if (now - pending.LastSentAt < RetryInterval)
            {
                continue;
            }

            pending.LastSentAt = now;
            pending.Attempts++;
            SendDepositRequest(pending);
            LogDiagnostic($"client_retry tx={pending.TransactionId} chest={pending.ContainerId} attempt={pending.Attempts}");
        }
    }

    private static void SendDepositRequest(PendingDeposit pending)
    {
        ZRoutedRpc.instance.InvokeRoutedRPC(DepositRequestRpc, new ZPackage(pending.RequestBytes));
    }

    private static bool TryGetContainerId(Container container, out ZDOID id)
    {
        id = ZDOID.None;
        ZNetView? view = NetworkViewField.GetValue(container) as ZNetView;
        if (!view || !view.IsValid())
        {
            return false;
        }

        id = view.GetZDO().m_uid;
        return !id.IsNone();
    }

    private static void Restore(Inventory inventory, IEnumerable<ReservedDepositItem> items)
    {
        foreach (ReservedDepositItem item in items)
        {
            RestoreRemainder(inventory, item, item.Item.m_stack);
        }
    }

    private static void RestoreRemainder(Inventory inventory, ReservedDepositItem reserved, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        ItemDrop.ItemData remainder = reserved.Item.Clone();
        remainder.m_stack = amount;
        if (inventory.AddItem(remainder, reserved.SourcePosition) || inventory.AddItem(remainder))
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player)
        {
            ItemDrop.DropItem(
                remainder,
                remainder.m_stack,
                player.transform.position + player.transform.forward + Vector3.up,
                player.transform.rotation);
        }
        LogWarning($"client_restore_dropped item={remainder.m_shared.m_name} amount={remainder.m_stack}");
    }
}
