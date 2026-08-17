using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BenheimInventoryProtocol;

internal static partial class InventoryTransactions
{
    private const float CompletedRetention = 600f;

    private static void RpcDepositRequest(long sender, ZPackage request)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        byte[] requestBytes = request.GetArray();
        string payloadHash = InventoryTransactionWire.Hash(requestBytes);
        if (!TryPeekRequest(requestBytes, out string transactionId, out ZDOID containerId))
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "server_request_rejected",
                        "server_router",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "routing")
                    .Code("status", "rejected")
                    .Code("reason", "invalid_request"));
            return;
        }

        long owner = ResolveOwner(containerId);
        List<RequestedDepositItem> routedItems = new List<RequestedDepositItem>();
        InventoryTransactionWire.TryReadRequest(
            requestBytes,
            out _,
            out _,
            out _,
            out _,
            out routedItems);
        ServerRequestDecision decision = ServerRouter.ReceiveRequest(
            transactionId,
            sender,
            payloadHash,
            requestBytes,
            containerId,
            owner);
        if (decision.Action == ServerRequestAction.Replay)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create("server_result_replayed", "server_router")
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(containerId))
                    .Code("operation_phase", "routing")
                    .Code("status", "replayed")
                    .Code("reason", "completed_cache")
                    .Integer("requester_peer", sender)
                    .Integer("requested_count", CountRequested(routedItems))
                    .Text("requested_items", DescribeRequested(routedItems)));
            SendClientResult(sender, decision.ResponseBytes!);
            return;
        }

        if (decision.Action == ServerRequestAction.Conflict)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "server_request_rejected",
                        "server_router",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(containerId))
                    .Code("operation_phase", "routing")
                    .Code("status", "conflict")
                    .Code("reason", "transaction_conflict")
                    .Integer("requester_peer", sender)
                    .Integer("requested_count", CountRequested(routedItems))
                    .Text("requested_items", DescribeRequested(routedItems)));
            SendClientResult(sender, ConflictResponse(transactionId, payloadHash));
            return;
        }

        if (decision.Action == ServerRequestAction.OwnerUnavailable)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "server_owner_unavailable",
                        "server_router",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(containerId))
                    .Code("operation_phase", "routing")
                    .Code("status", "pending")
                    .Code("reason", "owner_unavailable")
                    .Integer("requester_peer", sender)
                    .Integer("requested_count", CountRequested(routedItems))
                    .Text("requested_items", DescribeRequested(routedItems)));
            return;
        }

        ZPackage envelope = new ZPackage();
        envelope.Write(sender);
        envelope.Write(new ZPackage(requestBytes));
        ZRoutedRpc.instance.InvokeRoutedRPC(decision.Owner, OwnerExecuteRpc, envelope);
        Emit(
            InventoryTransactionDiagnosticEvent.Create(
                    decision.Rerouted ? "server_rerouted" : "server_routed",
                    "server_router")
                .Code("correlation", transactionId)
                .Code("chest_id", StableChestId(containerId))
                .Code("operation_phase", "routing")
                .Code("status", "sent")
                .Code("reason", decision.Rerouted ? "owner_changed" : "current_owner")
                .Integer("requester_peer", sender)
                .Integer("owner_peer", decision.Owner)
                .Integer("requested_count", CountRequested(routedItems))
                .Text("requested_items", DescribeRequested(routedItems))
                .Boolean("rerouted", decision.Rerouted));
    }

    private static void RpcOwnerResult(long sender, ZPackage envelope)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        long requester = envelope.ReadLong();
        ZPackage response = envelope.ReadPackage();
        byte[] responseBytes = response.GetArray();
        if (!InventoryTransactionWire.TryReadResponse(
                new ZPackage(responseBytes),
                out string transactionId,
                out string payloadHash,
                out DepositStatus status,
                out List<int> accepted)
            || !ServerRouter.TryGetPendingContainer(transactionId, out ZDOID containerId))
        {
            InventoryTransactionDiagnosticEvent rejected =
                InventoryTransactionDiagnosticEvent.Create(
                        "server_result_rejected",
                        "server_router",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "result")
                    .Code("status", "rejected")
                    .Code("reason", "invalid_or_unknown_result");
            if (!string.IsNullOrEmpty(transactionId))
            {
                rejected.Code("correlation", transactionId);
            }
            Emit(rejected);
            return;
        }

        long currentOwner = ResolveOwner(containerId);
        OwnerResultAction action = ServerRouter.ReceiveOwnerResult(
            transactionId,
            requester,
            payloadHash,
            sender,
            currentOwner,
            responseBytes,
            Time.realtimeSinceStartup,
            status == DepositStatus.StaleOwner);
        if (action == OwnerResultAction.Reject)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "server_result_rejected",
                        "server_router",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(containerId))
                    .Code("operation_phase", "result")
                    .Code("status", "rejected")
                    .Code("reason", "not_current_routed_owner")
                    .Integer("requester_peer", requester)
                    .Integer("owner_peer", sender)
                    .Integer("accepted_count", CountAccepted(accepted)));
            return;
        }

        if (action == OwnerResultAction.AwaitRetry)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "server_stale_owner",
                        "server_router",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(containerId))
                    .Code("operation_phase", "result")
                    .Code("status", "pending")
                    .Code("reason", "owner_reported_stale")
                    .Integer("requester_peer", requester)
                    .Integer("owner_peer", sender));
            return;
        }

        Emit(
            InventoryTransactionDiagnosticEvent.Create("server_result_forwarded", "server_router")
                .Code("correlation", transactionId)
                .Code("chest_id", StableChestId(containerId))
                .Code("operation_phase", "result")
                .Code("status", StatusCode(status))
                .Integer("requester_peer", requester)
                .Integer("owner_peer", sender)
                .Integer("accepted_count", CountAccepted(accepted)));
        SendClientResult(requester, responseBytes);
    }

    private static void RpcReceiptAck(long sender, ZPackage acknowledgement)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        try
        {
            string transactionId = acknowledgement.ReadString();
            string payloadHash = acknowledgement.ReadString();
            ZDOID containerId = acknowledgement.ReadZDOID();
            byte[] requestBytes = acknowledgement.ReadByteArray();
            if (payloadHash != InventoryTransactionWire.Hash(requestBytes)
                || !InventoryTransactionWire.TryReadRequest(
                    requestBytes,
                    out _,
                    out string requestTransactionId,
                    out long requestPlayerId,
                    out ZDOID requestContainerId,
                    out _)
                || requestTransactionId != transactionId
                || requestContainerId != containerId
                || !RequestBelongsToSender(sender, requestPlayerId)
                || !ServerRouter.MatchesCompleted(
                    transactionId,
                    sender,
                    payloadHash,
                    containerId))
            {
                Emit(
                    InventoryTransactionDiagnosticEvent.Create(
                            "server_receipt_ack_rejected",
                            "server_router",
                            InventoryTransactionDiagnosticLevel.Warning)
                        .Code("operation_phase", "receipt_ack")
                        .Code("status", "rejected")
                        .Code("reason", "invalid_ack"));
                return;
            }

            long owner = ResolveOwner(containerId);
            if (owner == 0L)
            {
                Emit(
                    InventoryTransactionDiagnosticEvent.Create(
                            "server_receipt_ack_rejected",
                            "server_router",
                            InventoryTransactionDiagnosticLevel.Warning)
                        .Code("correlation", transactionId)
                        .Code("chest_id", StableChestId(containerId))
                        .Code("operation_phase", "receipt_ack")
                        .Code("status", "pending")
                        .Code("reason", "owner_unavailable"));
                return;
            }

            ZPackage ownerAcknowledgement = new ZPackage();
            ownerAcknowledgement.Write(sender);
            ownerAcknowledgement.Write(transactionId);
            ownerAcknowledgement.Write(payloadHash);
            ownerAcknowledgement.Write(containerId);
            ZRoutedRpc.instance.InvokeRoutedRPC(owner, OwnerReceiptAckRpc, ownerAcknowledgement);
            Emit(
                InventoryTransactionDiagnosticEvent.Create("server_receipt_ack_routed", "server_router")
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(containerId))
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "sent")
                    .Integer("requester_peer", sender)
                    .Integer("owner_peer", owner));
        }
        catch (Exception ex)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "server_receipt_ack_rejected",
                        "server_router",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "rejected")
                    .Code("reason", "invalid_ack")
                    .Code("exception_type", ex.GetType().Name));
        }
    }

    private static void RpcOwnerReceiptAckResult(long sender, ZPackage acknowledgement)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        try
        {
            long requester = acknowledgement.ReadLong();
            string transactionId = acknowledgement.ReadString();
            string payloadHash = acknowledgement.ReadString();
            ZDOID containerId = acknowledgement.ReadZDOID();
            long currentOwner = ResolveOwner(containerId);
            if (sender == 0L
                || sender != currentOwner
                || !ServerRouter.MatchesCompleted(
                    transactionId,
                    requester,
                    payloadHash,
                    containerId))
            {
                Emit(
                    InventoryTransactionDiagnosticEvent.Create(
                            "server_receipt_ack_result_rejected",
                            "server_router",
                            InventoryTransactionDiagnosticLevel.Warning)
                        .Code("correlation", transactionId)
                        .Code("chest_id", StableChestId(containerId))
                        .Code("operation_phase", "receipt_ack")
                        .Code("status", "rejected")
                        .Code("reason", "not_current_owner_or_completed_result")
                        .Integer("requester_peer", requester)
                        .Integer("owner_peer", sender));
                return;
            }

            ServerRouter.MarkReceiptAcknowledged(
                transactionId,
                requester,
                payloadHash,
                containerId,
                Time.realtimeSinceStartup);

            ZPackage clientAcknowledgement = new ZPackage();
            clientAcknowledgement.Write(transactionId);
            clientAcknowledgement.Write(payloadHash);
            ZRoutedRpc.instance.InvokeRoutedRPC(
                requester,
                ReceiptAckResultRpc,
                clientAcknowledgement);
            Emit(
                InventoryTransactionDiagnosticEvent.Create("server_receipt_ack_confirmed", "server_router")
                    .Code("correlation", transactionId)
                    .Code("chest_id", StableChestId(containerId))
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "confirmed")
                    .Integer("requester_peer", requester)
                    .Integer("owner_peer", sender));
        }
        catch (Exception ex)
        {
            Emit(
                InventoryTransactionDiagnosticEvent.Create(
                        "server_receipt_ack_result_rejected",
                        "server_router",
                        InventoryTransactionDiagnosticLevel.Warning)
                    .Code("operation_phase", "receipt_ack")
                    .Code("status", "rejected")
                    .Code("reason", "invalid_ack_result")
                    .Code("exception_type", ex.GetType().Name));
        }
    }

    private static bool RequestBelongsToSender(long sender, long playerId)
    {
        Player? requester = Player.GetAllPlayers().Find(
            player => player && player.GetOwner() == sender);
        return requester && requester.GetPlayerID() == playerId;
    }

    private static void SendClientResult(long requester, byte[] responseBytes)
    {
        ZRoutedRpc.instance.InvokeRoutedRPC(requester, DepositResultRpc, new ZPackage(responseBytes));
    }

    private static void ExpireServerResults(float now)
    {
        ServerRouter.ExpireCompleted(now - CompletedRetention);
    }

    private static bool TryPeekRequest(byte[] requestBytes, out string transactionId, out ZDOID containerId)
    {
        transactionId = string.Empty;
        containerId = ZDOID.None;
        try
        {
            ZPackage request = new ZPackage(requestBytes);
            if (request.ReadInt() != ProtocolVersion)
            {
                return false;
            }

            transactionId = request.ReadString();
            request.ReadLong();
            containerId = request.ReadZDOID();
            return transactionId.Length == 32 && !containerId.IsNone();
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static long ResolveOwner(ZDOID containerId)
    {
        ZDO? zdo = ZDOMan.instance?.GetZDO(containerId);
        return zdo?.GetOwner() ?? 0L;
    }

    private static byte[] ConflictResponse(string transactionId, string payloadHash)
    {
        return EmptyResponse(transactionId, payloadHash, DepositStatus.TransactionConflict);
    }

    private static byte[] EmptyResponse(string transactionId, string payloadHash, DepositStatus status)
    {
        return InventoryTransactionWire.BuildResponse(
            transactionId,
            payloadHash,
            status,
            Array.Empty<int>()).GetArray();
    }
}
