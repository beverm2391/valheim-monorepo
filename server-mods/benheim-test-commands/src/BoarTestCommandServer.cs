using BenheimQoL.EnemyTiers;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using System;
using UnityEngine;

namespace BenheimTestCommands;

[HarmonyPatch]
internal static class BoarTestCommandServer
{
    private const string BoarPrefabName = "Boar";
    private static readonly Vector3 SpawnOffset = new Vector3(0f, 1f, 2f);

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    [HarmonyPostfix]
    private static void AfterNewConnection(ZNetPeer peer)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        // This direct connection RPC deliberately mirrors native Save's trust
        // boundary. The callback receives the authenticated socket instead of
        // trusting the sender ID carried inside a routed-RPC envelope.
        peer.m_rpc.Register<string, int>(
            BoarTestCommandProtocol.RequestRpc,
            (rpc, operationId, stars) => OnRequest(peer, rpc, operationId, stars));
    }

    private static void OnRequest(ZNetPeer peer, ZRpc rpc, string operationId, int stars)
    {
        string safeOperationId = Guid.TryParseExact(operationId, "N", out _)
            ? operationId
            : "invalid";
        string requester = string.IsNullOrWhiteSpace(peer.m_playerName)
            ? "unresolved"
            : Flatten(peer.m_playerName);

        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_requested")
                .String("operation_id", safeOperationId)
                .String("operation_phase", "start")
                .String("requester", requester)
                .Integer("stars", stars));

        if (safeOperationId == "invalid")
        {
            Reject(rpc, safeOperationId, requester, stars, "invalid_operation_id");
            return;
        }

        if (!BoarTestCommandProtocol.TryResolveLevel(stars, out int level))
        {
            Reject(rpc, safeOperationId, requester, stars, "unsupported_star_count");
            return;
        }

        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            Reject(rpc, safeOperationId, requester, stars, "not_server");
            return;
        }

        if (!ReferenceEquals(rpc, peer.m_rpc) || !peer.IsReady() || peer.m_socket == null)
        {
            Reject(rpc, safeOperationId, requester, stars, "requester_not_ready");
            return;
        }

        if (!ZNet.instance.IsAdmin(rpc.GetSocket().GetHostName()))
        {
            Reject(rpc, safeOperationId, requester, stars, "not_admin");
            return;
        }

        if (!IsFinite(peer.m_refPos))
        {
            Reject(rpc, safeOperationId, requester, stars, "invalid_player_position");
            return;
        }

        ZNetScene? scene = ZNetScene.instance;
        if (scene == null)
        {
            Reject(rpc, safeOperationId, requester, stars, "native_scene_unavailable");
            return;
        }

        GameObject? prefab = scene.GetPrefab(BoarPrefabName);
        if (prefab == null)
        {
            Reject(rpc, safeOperationId, requester, stars, "native_boar_unavailable");
            return;
        }

        GameObject? spawned = null;
        try
        {
            spawned = UnityEngine.Object.Instantiate(
                prefab,
                peer.m_refPos + SpawnOffset,
                Quaternion.identity);
            Character? character = spawned.GetComponent<Character>();
            if (character == null)
            {
                scene.Destroy(spawned);
                Reject(rpc, safeOperationId, requester, stars, "native_boar_missing_character");
                return;
            }

            character.SetLevel(level);
        }
        catch (Exception exception)
        {
            if (spawned != null)
            {
                // Instantiate creates the persistent ZDO in ZNetView.Awake.
                // Native network destruction must remove both the object and
                // that ZDO before this spawn transaction can reject safely.
                scene.Destroy(spawned);
            }
            Reject(rpc, safeOperationId, requester, stars, $"spawn_failed_{exception.GetType().Name}");
            return;
        }

        // The authoritative spawn is committed once native SetLevel returns.
        // Result delivery is intentionally outside that transaction: a socket
        // failure must not destroy a valid network creature or leave its ZDO
        // behind. The client timeout makes a missing result visible.
        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_accepted")
                .String("operation_id", safeOperationId)
                .String("operation_phase", "terminal")
                .String("requester", requester)
                .String("prefab", BoarPrefabName)
                .Integer("stars", stars)
                .Integer("level", level));
        TrySendResult(rpc, safeOperationId, "accepted", "spawned", level);
    }

    private static void Reject(ZRpc rpc, string operationId, string requester, int stars, string reason)
    {
        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_rejected")
                .String("operation_id", operationId)
                .String("operation_phase", "terminal")
                .String("requester", requester)
                .Integer("stars", stars)
                .String("reason", reason));
        TrySendResult(rpc, operationId, "rejected", reason, 0);
    }

    private static void TrySendResult(ZRpc rpc, string operationId, string outcome, string reason, int level)
    {
        try
        {
            rpc.Invoke(
                BoarTestCommandProtocol.ResultRpc,
                operationId,
                outcome,
                reason,
                level);
        }
        catch (Exception exception)
        {
            ServerDiagnostics.Emit(
                DiagnosticEvent.Create("EnemyTiers", "boar_test_spawn_result_delivery_failed")
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
