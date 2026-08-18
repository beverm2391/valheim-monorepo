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
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "owner_request_rejected",
                        "chest_owner",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "validation")
                    .Code("status", "rejected")
                    .Code("reason", "sender_not_server"));
            return;
        }

        long requester = envelope.ReadLong();
        ZPackage request = envelope.ReadPackage();
        byte[] requestBytes = request.GetArray();
        string payloadHash = InventoryTransactionWire.Hash(requestBytes);
        if (!InventoryTransactionWire.TryReadRequest(
                requestBytes,
                out _,
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
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "owner_validation_result",
                        "chest_owner",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "validation")
                    .Code("status", "invalid_request")
                    .Code("reason", "invalid_request")
                    .Integer("requested_count", 0));
            return;
        }

        if (!TryResolveOwnedContainer(containerId, out Container? container, out ZDO? zdo))
        {
            SendResult(requester, InventoryTransactionWire.BuildResponse(
                transactionId,
                payloadHash,
                DepositStatus.StaleOwner,
                Array.Empty<int>()));
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "owner_validation_result",
                        "chest_owner",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                    .Code("operation_phase", "validation")
                    .Code("status", "stale_owner")
                    .Code("reason", "not_current_owner")
                    .Integer("requester_peer", requester)
                    .Integer("owner_peer", zdo?.GetOwner() ?? 0L)
                    .Integer("requested_count", InventoryTransactions.CountRequested(requestedItems))
                    .Text("requested_items", InventoryTransactions.DescribeRequested(requestedItems)));
            return;
        }

        if (InventoryTransactionReceipts.TryRead(
                zdo!, transactionId, payloadHash, out DepositStatus cachedStatus, out List<int> cachedAccepted))
        {
            SendResult(requester, InventoryTransactionWire.BuildResponse(
                transactionId, payloadHash, cachedStatus, cachedAccepted));
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create("owner_duplicate", "chest_owner")
                    .Code("correlation", transactionId)
                    .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                    .Code("operation_phase", "owner_apply")
                    .Code("status", StatusCode(cachedStatus))
                    .Code("reason", "receipt_replay")
                    .Integer("requester_peer", requester)
                    .Integer("owner_peer", zdo!.GetOwner())
                    .Integer("revision_after", zdo.DataRevision)
                    .Integer("requested_count", InventoryTransactions.CountRequested(requestedItems))
                    .Integer("accepted_count", InventoryTransactions.CountAccepted(cachedAccepted))
                    .Number("chest_position_x", container!.transform.position.x)
                    .Number("chest_position_y", container.transform.position.y)
                    .Number("chest_position_z", container.transform.position.z)
                    .Text("requested_items", InventoryTransactions.DescribeRequested(requestedItems))
                    .Text("accepted_items", InventoryTransactions.DescribeAccepted(requestedItems, cachedAccepted))
                    .Text("contents_after", InventoryTransactions.DescribeInventory(container!.GetInventory())));
            return;
        }

        int itemCount = requestedItems.Count;
        if (!InventoryTransactionReceipts.CanRecord(zdo!, transactionId))
        {
            SendResult(requester, InventoryTransactionWire.BuildResponse(
                transactionId,
                payloadHash,
                DepositStatus.ReceiptCapacity,
                Zeroes(itemCount)));
            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "owner_receipt_capacity",
                        "chest_owner",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                    .Code("operation_phase", "owner_apply")
                    .Code("status", "rejected")
                    .Code("reason", "receipt_capacity")
                    .Integer("requester_peer", requester)
                    .Integer("owner_peer", zdo!.GetOwner())
                    .Integer("revision_before", zdo.DataRevision)
                    .Integer("requested_count", InventoryTransactions.CountRequested(requestedItems))
                    .Number("chest_position_x", container!.transform.position.x)
                    .Number("chest_position_y", container.transform.position.y)
                    .Number("chest_position_z", container.transform.position.z)
                    .Text("requested_items", InventoryTransactions.DescribeRequested(requestedItems))
                    .Text("contents_before", InventoryTransactions.DescribeInventory(container!.GetInventory())));
            return;
        }

        long revisionBefore = zdo!.DataRevision;
        string contentsBefore = InventoryTransactions.DescribeInventory(container!.GetInventory());
        DepositStatus validation = Validate(
            container!,
            requester,
            playerId,
            itemCount,
            out string reason,
            out string exceptionType);
        bool fullyApplied = true;
        List<int> accepted = validation == DepositStatus.Success
            ? ApplyDeposit(container!, requestedItems, transactionId, containerId, out fullyApplied)
            : Zeroes(itemCount);
        if (validation == DepositStatus.Success && !fullyApplied)
        {
            validation = DepositStatus.Rejected;
            reason = "native_apply_partial";
        }

        InventoryTransactionReceipts.Record(zdo!, transactionId, payloadHash, validation, accepted);
        SendResult(requester, InventoryTransactionWire.BuildResponse(
            transactionId, payloadHash, validation, accepted));
        InventoryTransactionDiagnosticEvent resultEvent =
            InventoryTransactionDiagnosticEvent.Create("owner_result", "chest_owner")
                .Code("correlation", transactionId)
                .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                .Code("operation_phase", "owner_apply")
                .Code("status", StatusCode(validation))
                .Code("reason", reason)
                .Integer("requester_peer", requester)
                .Integer("owner_peer", zdo.GetOwner())
                .Integer("revision_before", revisionBefore)
                .Integer("revision_after", zdo.DataRevision)
                .Integer("requested_count", InventoryTransactions.CountRequested(requestedItems))
                .Integer("accepted_count", InventoryTransactions.CountAccepted(accepted))
                .Number("chest_position_x", container.transform.position.x)
                .Number("chest_position_y", container.transform.position.y)
                .Number("chest_position_z", container.transform.position.z)
                .Text("requested_items", InventoryTransactions.DescribeRequested(requestedItems))
                .Text("accepted_items", InventoryTransactions.DescribeAccepted(requestedItems, accepted))
                .Text("contents_before", contentsBefore)
                .Text("contents_after", InventoryTransactions.DescribeInventory(container.GetInventory()));
        if (!string.IsNullOrEmpty(exceptionType))
        {
            resultEvent.Code("exception_type", exceptionType);
        }
        InventoryTransactions.Emit(resultEvent);
    }

    private static DepositStatus Validate(
        Container container,
        long requester,
        long playerId,
        int itemCount,
        out string reason,
        out string exceptionType)
    {
        exceptionType = string.Empty;
        if (itemCount <= 0 || itemCount > InventoryTransactions.MaxItemsPerDeposit)
        {
            reason = "item_count_invalid";
            return DepositStatus.InvalidRequest;
        }

        try
        {
            bool hasAccess = (bool)(CheckAccessMethod.Invoke(container, new object[] { playerId }) ?? false);
            if (!hasAccess)
            {
                reason = "ward_access_denied";
                return DepositStatus.AccessDenied;
            }
        }
        catch (Exception ex)
        {
            reason = "access_check_exception";
            exceptionType = ex.GetType().Name;
            return DepositStatus.InvalidRequest;
        }

        Player? requesterPlayer = Player.GetAllPlayers().Find(player => player && player.GetOwner() == requester);
        if (!requesterPlayer)
        {
            reason = "requester_unavailable";
            return DepositStatus.AccessDenied;
        }

        if (requesterPlayer.GetPlayerID() != playerId)
        {
            reason = "player_identity_mismatch";
            return DepositStatus.AccessDenied;
        }

        if (Vector3.SqrMagnitude(requesterPlayer.transform.position - container.transform.position)
            > MaxDistance * MaxDistance)
        {
            reason = "out_of_range";
            return DepositStatus.AccessDenied;
        }

        if (container.IsInUse() && GetOwner(container) != requester)
        {
            reason = "chest_in_use";
            return DepositStatus.Rejected;
        }

        reason = "accepted";
        return DepositStatus.Success;
    }

    private static List<int> ApplyDeposit(
        Container container,
        List<RequestedDepositItem> requestedItems,
        string transactionId,
        ZDOID containerId,
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
                InventoryTransactions.Emit(
                    InventoryTransactionDiagnosticEvent.Create(
                            "owner_apply_partial",
                            "chest_owner",
                            InventoryTransactionDiagnosticLevel.Warning)
                        .Code("correlation", transactionId)
                        .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                        .Code("operation_phase", "owner_apply")
                        .Code("status", "partial")
                        .Code("reason", "native_add_exception")
                        .Code("exception_type", ex.GetType().Name)
                        .Integer("requested_count", InventoryTransactions.CountRequested(requestedItems))
                        .Integer("accepted_count", InventoryTransactions.CountAccepted(accepted))
                        .Text("requested_items", InventoryTransactions.DescribeRequested(requestedItems))
                        .Text("accepted_items", InventoryTransactions.DescribeAccepted(requestedItems, accepted))
                        .Text("contents_after", InventoryTransactions.DescribeInventory(target)));
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
            InventoryTransactionProtocol.OwnerResultRpc,
            envelope);
    }

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
                    InventoryTransactionDiagnosticEvent.Create("owner_receipt_ack_rejected", "chest_owner",
                            InventoryTransactionDiagnosticLevel.Warning)
                        .Code("correlation", transactionId).Code("chest_id", InventoryTransactions.StableChestId(containerId))
                        .Code("operation_phase", "receipt_ack")
                        .Code("status", "ignored")
                        .Code("reason", "receipt_not_found"));
                return;
            }

            InventoryTransactions.Emit(
                InventoryTransactionDiagnosticEvent.Create("owner_receipt_acknowledged", "chest_owner")
                    .Code("correlation", transactionId)
                    .Code("chest_id", InventoryTransactions.StableChestId(containerId))
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "acknowledged")
                    .Integer("owner_peer", zdo!.GetOwner())
                    .Integer("revision_after", zdo.DataRevision)
                    .Text("contents_after", InventoryTransactions.DescribeInventory(container!.GetInventory())));
    }

    private static string StatusCode(DepositStatus status) =>
        status.ToString().ToLowerInvariant();
}
