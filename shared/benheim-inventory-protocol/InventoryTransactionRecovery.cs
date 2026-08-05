using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    private static void RecoverPendingJournals()
    {
        Player? player = Player.m_localPlayer;
        if (journalRecoveryAttempted || !player || Game.instance == null)
        {
            return;
        }

        journalRecoveryAttempted = true;
        Inventory source = player.GetInventory();
        long currentPlayerId = Game.instance.GetPlayerProfile().GetPlayerID();
        long currentWorldId = ZNet.instance.GetWorldUID();
        foreach (PendingJournalRecord record in InventoryTransactionJournal.ReadAll(
            currentPlayerId,
            currentWorldId))
        {
            if (ClientPending.ContainsKey(record.TransactionId)
                || ClientCompleted.ContainsKey(record.TransactionId))
            {
                continue;
            }

            if (!InventoryTransactionWire.TryReadRequest(
                    record.RequestBytes,
                    out int requestProtocol,
                    out string transactionId,
                    out long playerId,
                    out ZDOID containerId,
                    out List<RequestedDepositItem> requested)
                || transactionId != record.TransactionId
                || containerId != record.ContainerId
                || playerId != currentPlayerId)
            {
                LogWarning($"journal_recovery_blocked tx={record.TransactionId} reason=request_invalid");
                continue;
            }

            if (!InventoryTransactionRecoveryPolicy.TryChooseAction(
                    requestProtocol,
                    record.Phase,
                    requested.Count,
                    record.Accepted.Count,
                    out PendingJournalRecoveryAction action))
            {
                LogWarning(
                    $"journal_recovery_blocked tx={record.TransactionId} " +
                    $"reason=phase_invalid protocol={requestProtocol} phase={record.Phase} " +
                    $"requested={requested.Count} accepted={record.Accepted.Count}");
                continue;
            }

            List<ReservedDepositItem> reserved = requested
                .Select(item => new ReservedDepositItem(item.Item, item.SourcePosition))
                .ToList();
            PendingDeposit pending = new PendingDeposit(
                record.TransactionId,
                record.PayloadHash,
                record.ContainerId,
                record.RequestBytes,
                record.PlayerId,
                record.WorldId,
                source,
                reserved,
                _ => { },
                Time.realtimeSinceStartup);
            if (action == PendingJournalRecoveryAction.RestorePrepared)
            {
                RestoreMissingPreparedItems(source, requested);
                ClientCompleted.Add(record.TransactionId, pending);
                LogDiagnostic(
                    $"journal_rolled_back_prepared tx={record.TransactionId} protocol={requestProtocol}");
                continue;
            }

            if (action == PendingJournalRecoveryAction.FinalizeCompleted)
            {
                NormalizeCompletedItems(source, reserved, record.Accepted);
                ClientCompleted.Add(record.TransactionId, pending);
                LogDiagnostic(
                    $"journal_recovered_completed tx={record.TransactionId} protocol={requestProtocol}");
                continue;
            }

            ReestablishReservation(source, reserved);
            ClientPending.Add(record.TransactionId, pending);
            SendDepositRequest(pending);
            LogDiagnostic(
                $"journal_recovered_reserved tx={record.TransactionId} " +
                $"protocol={requestProtocol} items={reserved.Count}");
        }
    }

    private static void ReestablishReservation(
        Inventory inventory,
        List<ReservedDepositItem> reserved)
    {
        foreach (ReservedDepositItem expected in reserved)
        {
            ItemDrop.ItemData actual = inventory.GetItemAt(
                expected.SourcePosition.x,
                expected.SourcePosition.y);
            if (actual != null && SameItem(actual, expected.Item))
            {
                inventory.RemoveItem(actual, Math.Min(actual.m_stack, expected.Item.m_stack));
            }
        }
    }

    private static void NormalizeCompletedItems(
        Inventory inventory,
        List<ReservedDepositItem> reserved,
        List<int> accepted)
    {
        for (int index = 0; index < reserved.Count; index++)
        {
            ReservedDepositItem expected = reserved[index];
            int desired = expected.Item.m_stack - Mathf.Clamp(
                accepted[index],
                0,
                expected.Item.m_stack);
            ItemDrop.ItemData actual = inventory.GetItemAt(
                expected.SourcePosition.x,
                expected.SourcePosition.y);
            int present = actual != null && SameItem(actual, expected.Item)
                ? actual.m_stack
                : 0;
            if (present > desired && actual != null)
            {
                inventory.RemoveItem(actual, present - desired);
            }
            else if (present < desired)
            {
                RestoreRemainder(inventory, expected, desired - present);
            }
        }
    }

    private static void RestoreMissingPreparedItems(
        Inventory inventory,
        List<RequestedDepositItem> requested)
    {
        foreach (RequestedDepositItem expected in requested)
        {
            ItemDrop.ItemData actual = inventory.GetItemAt(
                expected.SourcePosition.x,
                expected.SourcePosition.y);
            int present = actual != null && SameItem(actual, expected.Item)
                ? actual.m_stack
                : 0;
            int missing = expected.Item.m_stack - present;
            if (missing > 0)
            {
                RestoreRemainder(
                    inventory,
                    new ReservedDepositItem(expected.Item, expected.SourcePosition),
                    missing);
            }
        }
    }

    private static bool SameItem(ItemDrop.ItemData left, ItemDrop.ItemData right)
    {
        return left.m_shared.m_name == right.m_shared.m_name
            && left.m_quality == right.m_quality
            && left.m_worldLevel == right.m_worldLevel;
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
                player!.transform.position + player.transform.forward + Vector3.up,
                player.transform.rotation);
        }
        LogWarning($"client_restore_dropped item={remainder.m_shared.m_name} amount={remainder.m_stack}");
    }
}
