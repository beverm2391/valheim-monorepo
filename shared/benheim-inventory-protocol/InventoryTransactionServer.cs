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

        if (ServerCompleted.TryGetValue(transactionId, out CompletedServerDeposit? completed))
        {
            SendClientResult(
                sender,
                completed.Requester == sender && completed.PayloadHash == payloadHash
                    ? completed.ResponseBytes
                    : ConflictResponse(transactionId, payloadHash));
            return;
        }

        if (ServerPending.TryGetValue(transactionId, out ServerDeposit? pending))
        {
            if (pending.Requester != sender || pending.PayloadHash != payloadHash)
            {
                SendClientResult(sender, ConflictResponse(transactionId, payloadHash));
                return;
            }
        }
        else
        {
            pending = new ServerDeposit(sender, payloadHash, requestBytes, Time.realtimeSinceStartup);
            ServerPending.Add(transactionId, pending);
        }

        // Once the client has reserved items, temporary topology changes are
        // ambiguous. Keep the transaction pending until a compatible owner can
        // consult the chest's durable receipt instead of telling the client to
        // restore items that may already have committed.
        if (!ServerAllReadyPeersCompatible() || !PeerHasProtocol(sender))
        {
            LogServerBlock(pending, transactionId, "protocol_not_ready");
            return;
        }

        long owner = ResolveOwner(containerId);
        if (owner == 0L)
        {
            LogServerBlock(pending, transactionId, "owner_unavailable");
            return;
        }
        if (!PeerHasProtocol(owner))
        {
            LogServerBlock(pending, transactionId, "owner_protocol_missing");
            return;
        }

        ZPackage envelope = new ZPackage();
        envelope.Write(sender);
        envelope.Write(new ZPackage(requestBytes));
        pending.LastBlockReason = string.Empty;
        pending.RoutedOwners.Add(owner);
        ZRoutedRpc.instance.InvokeRoutedRPC(owner, OwnerExecuteRpc, envelope);
        LogDiagnostic($"server_routed tx={transactionId} requester={sender} owner={owner} chest={containerId}");
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
            || !ServerPending.TryGetValue(transactionId, out ServerDeposit? pending)
            || pending.Requester != requester
            || !pending.RoutedOwners.Contains(sender)
            || pending.PayloadHash != payloadHash)
        {
            LogWarning($"server_result_rejected tx={transactionId} sender={sender} requester={requester}");
            return;
        }

        if (status == DepositStatus.StaleOwner)
        {
            LogDiagnostic($"server_owner_stale tx={transactionId} owner={sender}");
            return;
        }

        ServerPending.Remove(transactionId);
        ServerCompleted[transactionId] = new CompletedServerDeposit(
            requester,
            payloadHash,
            responseBytes,
            Time.realtimeSinceStartup);
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

            if (ServerCompleted.TryGetValue(transactionId, out CompletedServerDeposit? completed)
                && (completed.Requester != sender || completed.PayloadHash != payloadHash))
            {
                return;
            }

            long owner = ResolveOwner(containerId);
            if (owner == 0L || !PeerHasProtocol(owner))
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

    private static void LogServerBlock(
        ServerDeposit pending,
        string transactionId,
        string reason)
    {
        if (pending.LastBlockReason == reason)
        {
            return;
        }

        pending.LastBlockReason = reason;
        LogWarning($"server_pending tx={transactionId} reason={reason}");
    }

    private static void ExpireServerResults(float now)
    {
        foreach (string transactionId in ServerCompleted
            .Where(pair => now - pair.Value.CompletedAt > CompletedRetention)
            .Select(pair => pair.Key)
            .ToList())
        {
            ServerCompleted.Remove(transactionId);
        }
    }

    private static bool TryPeekRequest(byte[] requestBytes, out string transactionId, out ZDOID containerId)
    {
        transactionId = string.Empty;
        containerId = ZDOID.None;
        try
        {
            ZPackage request = new ZPackage(requestBytes);
            if (!InventoryTransactionRecoveryPolicy.CanReadRequest(request.ReadInt()))
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

    private static bool PeerHasProtocol(long peerId)
    {
        if (peerId == ZNet.GetUID())
        {
            return true;
        }

        ZNetPeer? peer = ZNet.instance?.GetPeer(peerId);
        return peer != null
            && PeerCapabilities.TryGet(peerId, peer, out InventoryPeerAdvertisement advertised)
            && advertised.ProtocolVersion == ProtocolVersion;
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
