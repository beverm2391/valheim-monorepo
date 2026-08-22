using BenheimQoL.EnemyTiers;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BenheimTestCommands;

[HarmonyPatch]
internal static class HengeOverlayServer
{
    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    [HarmonyPostfix]
    private static void AfterNewConnection(ZNetPeer peer)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        // Use the authenticated connection directly, matching the existing
        // spawn-command boundary and Valheim's native Save authorization.
        peer.m_rpc.Register<string>(
            HengeOverlayProtocol.RequestRpc,
            (rpc, operationId) => OnRequest(peer, rpc, operationId));
    }

    private static void OnRequest(ZNetPeer peer, ZRpc rpc, string operationId)
    {
        string safeOperationId = Guid.TryParseExact(operationId, "N", out _)
            ? operationId
            : "invalid";
        string requester = string.IsNullOrWhiteSpace(peer.m_playerName)
            ? "unresolved"
            : Flatten(peer.m_playerName);

        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("TestCommands", "henge_overlay_requested")
                .String("operation_id", safeOperationId)
                .String("operation_phase", "start")
                .String("requester", requester));

        if (safeOperationId == "invalid")
        {
            Reject(rpc, safeOperationId, requester, "invalid_operation_id");
            return;
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            Reject(rpc, safeOperationId, requester, "not_server");
            return;
        }

        if (!ReferenceEquals(rpc, peer.m_rpc) || !peer.IsReady() || peer.m_socket == null)
        {
            Reject(rpc, safeOperationId, requester, "requester_not_ready");
            return;
        }

        if (!ZNet.instance.IsAdmin(rpc.GetSocket().GetHostName()))
        {
            Reject(rpc, safeOperationId, requester, "not_admin");
            return;
        }

        ZoneSystem? zoneSystem = ZoneSystem.instance;
        if (zoneSystem == null)
        {
            Reject(rpc, safeOperationId, requester, "native_zone_system_unavailable");
            return;
        }

        // LocationsGenerated is Valheim's readiness flag for the loaded or
        // completed native plan. Never start generation to satisfy a request.
        if (!zoneSystem.LocationsGenerated)
        {
            Reject(rpc, safeOperationId, requester, "native_location_plan_not_ready");
            return;
        }

        List<Vector3> coordinates = new List<Vector3>();
        foreach (ZoneSystem.LocationInstance location in zoneSystem.GetLocationList())
        {
            if (location.m_location == null)
            {
                Reject(rpc, safeOperationId, requester, "native_location_plan_invalid");
                return;
            }

            if (!HengeOverlayProtocol.IsHengeLocation(location.m_location.m_prefabName))
            {
                continue;
            }

            if (!IsFinite(location.m_position))
            {
                Reject(rpc, safeOperationId, requester, "native_location_plan_invalid");
                return;
            }

            // Deliberately include both placed and unplaced native instances.
            coordinates.Add(location.m_position);
        }

        ZPackage payload = new ZPackage();
        payload.Write(coordinates.Count);
        foreach (Vector3 coordinate in coordinates)
        {
            payload.Write(coordinate);
        }

        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("TestCommands", "henge_overlay_accepted")
                .String("operation_id", safeOperationId)
                .String("operation_phase", "terminal")
                .String("requester", requester)
                .Integer("coordinate_count", coordinates.Count));
        TrySendResult(rpc, safeOperationId, "accepted", "coordinates_ready", payload);
    }

    private static void Reject(ZRpc rpc, string operationId, string requester, string reason)
    {
        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("TestCommands", "henge_overlay_rejected")
                .String("operation_id", operationId)
                .String("operation_phase", "terminal")
                .String("requester", requester)
                .String("reason", reason));
        ZPackage emptyPayload = new ZPackage();
        emptyPayload.Write(0);
        TrySendResult(rpc, operationId, "rejected", reason, emptyPayload);
    }

    private static void TrySendResult(
        ZRpc rpc,
        string operationId,
        string outcome,
        string reason,
        ZPackage payload)
    {
        try
        {
            rpc.Invoke(HengeOverlayProtocol.ResultRpc, operationId, outcome, reason, payload);
        }
        catch (Exception exception)
        {
            ServerDiagnostics.Emit(
                DiagnosticEvent.Create("TestCommands", "henge_overlay_result_delivery_failed")
                    .String("operation_id", operationId)
                    .String("operation_phase", "delivery")
                    .String("outcome", outcome)
                    .String("reason", exception.GetType().Name));
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static string Flatten(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ').Replace(' ', '_');
    }
}
