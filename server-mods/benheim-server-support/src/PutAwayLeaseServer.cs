using BenheimQoL.Infrastructure;
using BenheimQoL.InventoryFeature;
using HarmonyLib;
using System;

namespace BenheimServerSupport;

[HarmonyPatch]
internal static class PutAwayLeaseServer
{
    private static readonly PutAwayLeaseState<ZNetPeer> Lease = new PutAwayLeaseState<ZNetPeer>();

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    [HarmonyPostfix]
    private static void AfterNewConnection(ZNetPeer peer)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        // Direct connection callbacks bind the request to Valheim's authenticated
        // peer. No routed sender ID from the client is trusted.
        peer.m_rpc.Register<string>(
            PutAwayLeaseProtocol.RequestRpc,
            (rpc, operationId) => OnRequest(peer, rpc, operationId));
        peer.m_rpc.Register<string>(
            PutAwayLeaseProtocol.ReleaseRpc,
            (rpc, operationId) => OnRelease(peer, rpc, operationId));
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect), typeof(ZNetPeer))]
    [HarmonyPrefix]
    private static void BeforeDisconnect(ZNetPeer peer)
    {
        if (Lease.TryReleasePeer(peer, out string operationId))
        {
            Emit("put_away_lease_released", operationId, peer, "peer_disconnected");
        }
    }

    internal static void Reset() => Lease.Reset();

    private static void OnRequest(ZNetPeer peer, ZRpc rpc, string operationId)
    {
        string safeOperationId = Guid.TryParseExact(operationId, "N", out _)
            ? operationId
            : "invalid";
        Emit("put_away_lease_requested", safeOperationId, peer, "request_received");

        if (safeOperationId == "invalid")
        {
            Reject(rpc, safeOperationId, peer, "invalid_operation_id");
            return;
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            Reject(rpc, safeOperationId, peer, "not_server");
            return;
        }

        if (!ReferenceEquals(rpc, peer.m_rpc) || !peer.IsReady() || peer.m_socket == null)
        {
            Reject(rpc, safeOperationId, peer, "requester_not_ready");
            return;
        }

        if (!Lease.TryAcquire(peer, safeOperationId))
        {
            Reject(rpc, safeOperationId, peer, "busy");
            return;
        }

        Emit("put_away_lease_granted", safeOperationId, peer, "mutation_allowed");
        if (!TrySendResult(rpc, safeOperationId, PutAwayLeaseProtocol.Granted, "granted"))
        {
            // The requester cannot enter Put Away without the grant. Clear this
            // lease now instead of waiting for a disconnect that may never arrive.
            Lease.TryRelease(peer, safeOperationId);
            Emit("put_away_lease_released", safeOperationId, peer, "grant_delivery_failed");
        }
    }

    private static void OnRelease(ZNetPeer peer, ZRpc rpc, string operationId)
    {
        string safeOperationId = Guid.TryParseExact(operationId, "N", out _)
            ? operationId
            : "invalid";
        if (!ReferenceEquals(rpc, peer.m_rpc))
        {
            Emit("put_away_lease_release_rejected", safeOperationId, peer, "non_peer_sender");
            return;
        }

        if (!Lease.TryRelease(peer, safeOperationId))
        {
            Emit("put_away_lease_release_rejected", safeOperationId, peer, "not_active_owner");
            return;
        }

        Emit("put_away_lease_released", safeOperationId, peer, "client_terminal");
    }

    private static void Reject(ZRpc rpc, string operationId, ZNetPeer peer, string reason)
    {
        Emit("put_away_lease_rejected", operationId, peer, reason);
        TrySendResult(rpc, operationId, PutAwayLeaseProtocol.Rejected, reason);
    }

    private static bool TrySendResult(ZRpc rpc, string operationId, string outcome, string reason)
    {
        try
        {
            rpc.Invoke(PutAwayLeaseProtocol.ResultRpc, operationId, outcome, reason);
            return true;
        }
        catch (Exception exception)
        {
            ServerDiagnostics.Emit(
                DiagnosticEvent.Create("Inventory", "put_away_lease_result_delivery_failed")
                    .String("operation_id", operationId)
                    .String("operation_phase", "delivery")
                    .String("outcome", outcome)
                    .String("reason", exception.GetType().Name));
            return false;
        }
    }

    private static void Emit(string eventName, string operationId, ZNetPeer peer, string reason)
    {
        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("Inventory", eventName)
                .String("operation_id", operationId)
                .String("operation_phase", "lease")
                .Integer("requester", peer.m_uid)
                .String("reason", reason));
    }
}
