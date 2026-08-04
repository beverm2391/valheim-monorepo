using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimInventoryProtocol;

internal static class InventoryTransactionOwner
{
    private const float MaxDistance = 35f;
    private static readonly FieldInfo NetworkViewField = AccessTools.Field(typeof(Container), "m_nview");
    private static readonly MethodInfo CheckAccessMethod = AccessTools.Method(typeof(Container), "CheckAccess");

    internal static void Handle(long sender, ZPackage envelope)
    {
        if (!InventoryTransactions.IsExpectedServer(sender))
        {
            InventoryTransactions.LogWarning("owner_rejected reason=sender_not_server");
            return;
        }

        long requester = envelope.ReadLong();
        ZPackage request = envelope.ReadPackage();
        byte[] requestBytes = request.GetArray();
        string payloadHash = InventoryTransactionWire.Hash(requestBytes);
        if (!InventoryTransactionWire.TryReadRequest(
                requestBytes,
                out string transactionId,
                out long playerId,
                out ZDOID containerId,
                out List<RequestedDepositItem> requestedItems))
        {
            SendResult(requester, InventoryTransactionWire.BuildResponse(
                transactionId,
                payloadHash,
                DepositStatus.InvalidRequest,
                Array.Empty<int>()));
            return;
        }

        if (!TryResolveOwnedContainer(containerId, out Container? container, out ZDO? zdo))
        {
            SendResult(requester, InventoryTransactionWire.BuildResponse(
                transactionId,
                payloadHash,
                DepositStatus.StaleOwner,
                Array.Empty<int>()));
            return;
        }

        if (InventoryTransactionReceipts.TryRead(
                zdo!, transactionId, payloadHash, out DepositStatus cachedStatus, out List<int> cachedAccepted))
        {
            InventoryTransactions.LogDiagnostic(
                $"owner_duplicate tx={transactionId} status={cachedStatus} accepted={string.Join(",", cachedAccepted)}");
            SendResult(requester, InventoryTransactionWire.BuildResponse(
                transactionId, payloadHash, cachedStatus, cachedAccepted));
            return;
        }

        int itemCount = requestedItems.Count;
        if (!InventoryTransactionReceipts.CanRecord(zdo!, transactionId))
        {
            InventoryTransactions.LogWarning(
                $"owner_receipt_capacity tx={transactionId} chest={containerId}");
            SendResult(requester, InventoryTransactionWire.BuildResponse(
                transactionId,
                payloadHash,
                DepositStatus.ReceiptCapacity,
                Zeroes(itemCount)));
            return;
        }

        DepositStatus validation = Validate(container!, requester, playerId, itemCount);
        bool fullyApplied = true;
        List<int> accepted = validation == DepositStatus.Success
            ? ApplyDeposit(container!, requestedItems, out fullyApplied)
            : Zeroes(itemCount);
        if (validation == DepositStatus.Success && !fullyApplied)
        {
            validation = DepositStatus.Rejected;
        }

        InventoryTransactionReceipts.Record(zdo!, transactionId, payloadHash, validation, accepted);
        InventoryTransactions.LogDiagnostic(
            $"owner_result tx={transactionId} requester={requester} chest={containerId} " +
            $"status={validation} items=\"{InventoryTransactions.DescribeRequested(requestedItems, accepted)}\" " +
            $"revision={zdo!.DataRevision}");
        SendResult(requester, InventoryTransactionWire.BuildResponse(
            transactionId, payloadHash, validation, accepted));
    }

    private static DepositStatus Validate(Container container, long requester, long playerId, int itemCount)
    {
        if (itemCount <= 0 || itemCount > InventoryTransactions.MaxItemsPerDeposit)
        {
            return DepositStatus.InvalidRequest;
        }

        try
        {
            bool hasAccess = (bool)(CheckAccessMethod.Invoke(container, new object[] { playerId }) ?? false);
            if (!hasAccess)
            {
                return DepositStatus.AccessDenied;
            }
        }
        catch (Exception ex)
        {
            InventoryTransactions.LogWarning($"owner_access_check_failed error=\"{ex.Message}\"");
            return DepositStatus.InvalidRequest;
        }

        Player? requesterPlayer = Player.GetAllPlayers().Find(player => player && player.GetOwner() == requester);
        if (!requesterPlayer
            || requesterPlayer.GetPlayerID() != playerId
            || Vector3.SqrMagnitude(requesterPlayer.transform.position - container.transform.position)
                > MaxDistance * MaxDistance)
        {
            return DepositStatus.AccessDenied;
        }

        if (container.IsInUse() && GetOwner(container) != requester)
        {
            return DepositStatus.Rejected;
        }

        return DepositStatus.Success;
    }

    private static List<int> ApplyDeposit(
        Container container,
        List<RequestedDepositItem> requestedItems,
        out bool fullyApplied)
    {
        fullyApplied = true;
        Inventory target = container.GetInventory();
        HashSet<string> namesPresentBefore = new HashSet<string>();
        foreach (ItemDrop.ItemData stored in target.GetAllItems())
        {
            namesPresentBefore.Add(stored.m_shared.m_name);
        }

        List<int> accepted = new List<int>(requestedItems.Count);
        foreach (RequestedDepositItem requestedItem in requestedItems)
        {
            ItemDrop.ItemData item = requestedItem.Item;
            if (!namesPresentBefore.Contains(item.m_shared.m_name)
                || !target.CanAddItem(item, 1))
            {
                accepted.Add(0);
                continue;
            }

            int requested = item.m_stack;
            int before = CountMatching(target, item);
            try
            {
                target.AddItem(item.Clone());
                accepted.Add(Mathf.Clamp(CountMatching(target, item) - before, 0, requested));
            }
            catch (Exception ex)
            {
                // Earlier entries may already be persisted by Valheim. Preserve
                // every observable accepted amount instead of turning a partial
                // mutation into an all-zero response that duplicates on restore.
                accepted.Add(Mathf.Clamp(CountMatching(target, item) - before, 0, requested));
                while (accepted.Count < requestedItems.Count)
                {
                    accepted.Add(0);
                }

                fullyApplied = false;
                InventoryTransactions.LogWarning(
                    $"owner_apply_partial item={accepted.Count} error=\"{ex.Message}\"");
                break;
            }
        }

        return accepted;
    }

    private static int CountMatching(Inventory inventory, ItemDrop.ItemData item)
    {
        int count = 0;
        foreach (ItemDrop.ItemData stored in inventory.GetAllItems())
        {
            if (stored.m_shared.m_name == item.m_shared.m_name
                && stored.m_quality == item.m_quality
                && stored.m_worldLevel == item.m_worldLevel)
            {
                count += stored.m_stack;
            }
        }

        return count;
    }

    private static bool TryResolveOwnedContainer(ZDOID id, out Container? container, out ZDO? zdo)
    {
        container = null;
        zdo = ZDOMan.instance?.GetZDO(id);
        ZNetView? view = zdo != null ? ZNetScene.instance?.FindInstance(zdo) : null;
        if (!view || !view.IsOwner())
        {
            return false;
        }

        container = view.GetComponentInChildren<Container>();
        return container;
    }

    private static long GetOwner(Container container)
    {
        ZNetView? view = NetworkViewField.GetValue(container) as ZNetView;
        return view && view.IsValid() ? view.GetZDO().GetOwner() : 0L;
    }

    private static List<int> Zeroes(int count)
    {
        List<int> values = new List<int>(Math.Max(0, count));
        for (int index = 0; index < count; index++)
        {
            values.Add(0);
        }

        return values;
    }

    private static void SendResult(long requester, ZPackage response)
    {
        ZPackage envelope = new ZPackage();
        envelope.Write(requester);
        envelope.Write(response);
        ZRoutedRpc.instance.InvokeRoutedRPC(
            InventoryTransactions.GetServerPeerId(),
            InventoryTransactions.OwnerResultRpc,
            envelope);
    }

    internal static void HandleReceiptAck(long sender, ZPackage acknowledgement)
    {
        if (!InventoryTransactions.IsExpectedServer(sender))
        {
            return;
        }

        try
        {
            string transactionId = acknowledgement.ReadString();
            string payloadHash = acknowledgement.ReadString();
            ZDOID containerId = acknowledgement.ReadZDOID();
            if (!TryResolveOwnedContainer(containerId, out _, out ZDO? zdo))
            {
                return;
            }

            InventoryTransactionReceipts.Remove(
                zdo!,
                transactionId,
                payloadHash);
            InventoryTransactions.LogDiagnostic(
                $"owner_receipt_ack tx={transactionId} chest={containerId}");
        }
        catch (Exception ex)
        {
            InventoryTransactions.LogWarning($"owner_receipt_ack_invalid error=\"{ex.Message}\"");
        }
    }
}
