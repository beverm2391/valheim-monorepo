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
            return;
        }

        long owner = ResolveOwner(containerId);
        ServerRequestDecision decision = ServerRouter.ReceiveRequest(
            transactionId,
            sender,
            payloadHash,
            requestBytes,
            containerId,
            owner);
        if (decision.Action == ServerRequestAction.Replay)
        {
            SendClientResult(sender, decision.ResponseBytes!);
            return;
        }

        if (decision.Action == ServerRequestAction.Conflict)
        {
            SendClientResult(sender, ConflictResponse(transactionId, payloadHash));
            return;
        }

        if (decision.Action == ServerRequestAction.OwnerUnavailable)
        {
            LogWarning($"server_pending tx={transactionId} reason=owner_unavailable");
            return;
        }

        ZPackage envelope = new ZPackage();
        envelope.Write(sender);
        envelope.Write(new ZPackage(requestBytes));
        ZRoutedRpc.instance.InvokeRoutedRPC(decision.Owner, OwnerExecuteRpc, envelope);
        LogDiagnostic($"server_routed tx={transactionId} requester={sender} owner={decision.Owner} chest={containerId}");
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
                out _)
            || !ServerRouter.TryGetPendingContainer(transactionId, out ZDOID containerId))
        {
            LogWarning($"server_result_rejected tx={transactionId} sender={sender} requester={requester}");
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
            LogWarning(
                $"server_result_rejected tx={transactionId} sender={sender} requester={requester} " +
                $"current_owner={currentOwner} reason=not_current_routed_owner");
            return;
        }

        if (action == OwnerResultAction.AwaitRetry)
        {
            LogDiagnostic($"server_owner_stale tx={transactionId} owner={sender}");
            return;
        }

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
                || !RequestBelongsToSender(sender, requestPlayerId))
            {
                return;
            }

            long owner = ResolveOwner(containerId);
            if (owner == 0L)
            {
                return;
            }

            ZPackage ownerAcknowledgement = new ZPackage();
            ownerAcknowledgement.Write(transactionId);
            ownerAcknowledgement.Write(payloadHash);
            ownerAcknowledgement.Write(containerId);
            ZRoutedRpc.instance.InvokeRoutedRPC(owner, OwnerReceiptAckRpc, ownerAcknowledgement);
        }
        catch (Exception ex)
        {
            LogWarning($"server_receipt_ack_invalid sender={sender} error=\"{ex.Message}\"");
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
