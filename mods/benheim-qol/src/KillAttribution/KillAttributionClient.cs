using BenheimQoL.Infrastructure;
using BenheimQoL.PlayerCombat;
using HarmonyLib;
using System;
using UnityEngine;

namespace BenheimQoL.KillAttribution;

[HarmonyPatch]
internal static class KillAttributionClient
{
    private const float CapabilityTimeoutSeconds = 5f;
    private const float CapabilityRetryIntervalSeconds = 1f;

    private static ZRpc? connectionServerRpc;
    private static ZRpc? compatibleServerRpc;
    private static ZNetPeer? connectionPeer;
    private static bool deathResetPending;
    private static readonly KillAttributionCapabilityRetry CapabilityRetry =
        new KillAttributionCapabilityRetry(
            CapabilityTimeoutSeconds,
            CapabilityRetryIntervalSeconds);
    private static readonly KillChainDeliveryCursor ChainDelivery =
        new KillChainDeliveryCursor();

    internal static bool HasCompatibleServer =>
        compatibleServerRpc != null
        && compatibleServerRpc.IsConnected()
        && ReferenceEquals(compatibleServerRpc, ZNet.instance?.GetServerRPC());

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    [HarmonyPostfix]
    private static void AfterNewConnection(ZNetPeer peer)
    {
        if (ZNet.instance == null || ZNet.instance.IsServer() || !peer.m_server)
        {
            return;
        }

        connectionServerRpc = peer.m_rpc;
        compatibleServerRpc = null;
        connectionPeer = peer;
        deathResetPending = false;
        CapabilityRetry.Reset();
        ChainDelivery.Reset();
        peer.m_rpc.Register<int>(KillAttributionProtocol.CapabilityRpc, OnCapability);
        peer.m_rpc.Register<ZPackage>(KillAttributionProtocol.ConfirmedRpc, OnConfirmed);
        peer.m_rpc.Register<ZPackage>(
            KillAttributionProtocol.ChainTransitionRpc,
            OnChainTransition);
        peer.m_rpc.Register(
            KillAttributionProtocol.ChainResetAcknowledgedRpc,
            OnChainResetAcknowledged);
        EmitCapability(
            "registration",
            "registered",
            "server_response_handler",
            protocolVersion: KillAttributionProtocol.Version);
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    [HarmonyPrefix]
    private static void BeforeNetworkDestroy()
    {
        connectionServerRpc = null;
        compatibleServerRpc = null;
        connectionPeer = null;
        deathResetPending = false;
        CapabilityRetry.Reset();
        ChainDelivery.Reset();
    }

    internal static void Update()
    {
        float now = Time.realtimeSinceStartup;
        ZRpc? serverRpc = ZNet.instance?.GetServerRPC();
        if (serverRpc == null || !ReferenceEquals(serverRpc, connectionServerRpc))
        {
            return;
        }

        if (HasCompatibleServer || CapabilityRetry.Finished)
        {
            return;
        }

        if (!CapabilityRetry.Started)
        {
            CapabilityRetry.Begin(now);
            EmitCapability(
                "readiness",
                "accepted",
                "current_server_rpc_established",
                protocolVersion: KillAttributionProtocol.Version);
        }

        if (CapabilityRetry.TryBeginAttempt(now, out int attempt))
        {
            TryRequestCapability(serverRpc, attempt);
        }

        if (CapabilityRetry.HasTimedOut(now))
        {
            CapabilityRetry.Finish();
            HealthReporting.ReportKillAttributionUnavailable(
                $"no matching Kill Attribution V{KillAttributionProtocol.Version} capability was received");
            EmitCapability(
                "response",
                "rejected",
                "capability_timeout",
                protocolVersion: 0,
                attempt: CapabilityRetry.Attempts);
        }
    }

    internal static void Report(LethalHitObservation observation)
    {
        if (!observation.Eligible || ZNet.instance == null || ZNet.instance.IsServer())
        {
            return;
        }

        string operationId = Diagnostics.NewOperationId();
        ZRpc? serverRpc = ZNet.instance.GetServerRPC();
        if (serverRpc == null || !HasCompatibleServer)
        {
            EmitReport(operationId, observation, "not_sent", "compatible_server_unavailable");
            return;
        }

        if (KillAttributionRpcAttempt.TrySend(
                serverRpc.IsConnected(),
                () => serverRpc.Invoke(
                    KillAttributionProtocol.ReportRpc,
                    KillAttributionProtocol.BuildReport(
                        operationId,
                        observation.VictimId,
                        observation.KillerId)),
                out string failure))
        {
            EmitReport(operationId, observation, "sent", "owner_lethal_transition");
            return;
        }

        EmitReport(operationId, observation, "not_sent", failure);
    }

    internal static void ReportLocalDeath(Player player)
    {
        if (player != Player.m_localPlayer
            || ZNet.instance == null
            || ZNet.instance.IsServer())
        {
            return;
        }

        deathResetPending = true;

        ZRpc? serverRpc = ZNet.instance.GetServerRPC();
        if (serverRpc != null && HasCompatibleServer)
        {
            TrySendDeathReset(serverRpc);
        }
    }

    private static void OnCapability(ZRpc rpc, int version)
    {
        if (!ReferenceEquals(rpc, ZNet.instance?.GetServerRPC()))
        {
            EmitCapability(
                "response",
                "rejected",
                "non_current_server_rpc",
                version,
                CapabilityRetry.Attempts);
            return;
        }

        CapabilityRetry.Finish();
        bool protocolMatches = version == KillAttributionProtocol.Version;
        bool rpcConnected = rpc.IsConnected();
        compatibleServerRpc = protocolMatches && rpcConnected ? rpc : null;
        if (compatibleServerRpc != null)
        {
            HealthReporting.ReportKillAttributionAvailable();
            EmitCapability(
                "response",
                "accepted",
                "matching_protocol",
                version,
                CapabilityRetry.Attempts);
            if (deathResetPending)
            {
                TrySendDeathReset(rpc);
            }
        }
        else if (!protocolMatches)
        {
            HealthReporting.ReportKillAttributionUnavailable(
                $"server protocol {version} is incompatible with required V{KillAttributionProtocol.Version}");
            EmitCapability(
                "response",
                "rejected",
                "incompatible_protocol",
                version,
                CapabilityRetry.Attempts);
        }
        else
        {
            HealthReporting.ReportKillAttributionUnavailable(
                $"matching Kill Attribution V{KillAttributionProtocol.Version} capability arrived over a disconnected server RPC");
            EmitCapability(
                "response",
                "rejected",
                "rpc_disconnected",
                version,
                CapabilityRetry.Attempts);
        }
    }

    private static void TryRequestCapability(ZRpc serverRpc, int attempt)
    {
        if (KillAttributionRpcAttempt.TrySend(
                serverRpc.IsConnected(),
                () => serverRpc.Invoke(
                    KillAttributionProtocol.CapabilityRequestRpc,
                    KillAttributionProtocol.Version),
                out string failure))
        {
            EmitCapability(
                "request",
                "sent",
                "current_server_rpc",
                KillAttributionProtocol.Version,
                attempt);
            return;
        }

        EmitCapability(
            "request",
            "rejected",
            failure,
            KillAttributionProtocol.Version,
            attempt);
    }

    private static void EmitCapability(
        string phase,
        string status,
        string reason,
        int protocolVersion,
        int attempt = 0)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_feed_capability")
                .String("operation_phase", phase)
                .String("status", status)
                .String("reason", reason)
                .Integer("protocol_version", protocolVersion)
                .Integer("required_protocol_version", KillAttributionProtocol.Version)
                .Integer("attempt", attempt)
                .Integer("peer_uid", connectionPeer?.m_uid ?? 0L));
    }

    private static void OnConfirmed(ZRpc rpc, ZPackage package)
    {
        if (!ReferenceEquals(rpc, ZNet.instance?.GetServerRPC()))
        {
            EmitDeliveryRejected("non_server_sender");
            return;
        }

        if (!KillAttributionProtocol.TryReadConfirmation(package, out ConfirmedKillMessage message))
        {
            EmitDeliveryRejected("invalid_payload");
            return;
        }

        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null || localPlayer.GetZDOID() != message.KillerId)
        {
            EmitDeliveryRejected("killer_not_local_player");
            return;
        }

        PlayerCombatRuntime.Publish(
            new ConfirmedKill(
                PlayerCombatContext.Capture(localPlayer),
                message.KillerId,
                message.VictimId,
                message.VictimPrefabName,
                message.VictimPrefabHash,
                message.VictimLevel,
                message.VictimIsBoss,
                message.VictimIsTamed,
                message.Position,
                message.ServerSequence,
                message.ServerTimeSeconds));
    }

    private static void OnChainTransition(ZRpc rpc, ZPackage package)
    {
        if (!ReferenceEquals(rpc, ZNet.instance?.GetServerRPC()))
        {
            EmitDeliveryRejected("chain_non_server_sender");
            return;
        }

        if (!KillAttributionProtocol.TryReadChainTransition(
                package,
                out KillChainTransitionMessage message))
        {
            EmitDeliveryRejected("invalid_chain_payload");
            return;
        }

        Player? localPlayer = Player.m_localPlayer;
        if (localPlayer == null || localPlayer.GetZDOID() != message.KillerId)
        {
            EmitDeliveryRejected("chain_killer_not_local_player");
            return;
        }

        if (deathResetPending)
        {
            EmitDeliveryRejected("chain_transition_before_death_barrier");
            return;
        }

        if (!ChainDelivery.TryAccept(message.Kind, message.ServerSequence))
        {
            EmitDeliveryRejected("chain_sequence_not_new");
            return;
        }

        BerserkerChainTransitionKind kind = message.Kind switch
        {
            KillChainTransitionKind.Progressed => BerserkerChainTransitionKind.Progressed,
            KillChainTransitionKind.Activated => BerserkerChainTransitionKind.Activated,
            KillChainTransitionKind.Refreshed => BerserkerChainTransitionKind.Refreshed,
            KillChainTransitionKind.Escalated => BerserkerChainTransitionKind.Escalated,
            KillChainTransitionKind.Expired => BerserkerChainTransitionKind.Expired,
            _ => throw new ArgumentOutOfRangeException(nameof(message.Kind))
        };
        BerserkerChainTier tier = message.Tier switch
        {
            KillChainTier.None => BerserkerChainTier.None,
            KillChainTier.Berserker => BerserkerChainTier.Berserker,
            KillChainTier.Slaughterhouse => BerserkerChainTier.Slaughterhouse,
            _ => throw new ArgumentOutOfRangeException(nameof(message.Tier))
        };

        PlayerCombatRuntime.Publish(
            new BerserkerChainTransition(
                PlayerCombatContext.Capture(localPlayer),
                kind,
                tier,
                message.KillCount,
                message.ServerSequence,
                message.ServerTimeSeconds,
                message.ExpiresAtServerTimeSeconds));
    }

    private static void EmitReport(
        string operationId,
        LethalHitObservation observation,
        string status,
        string reason)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_report")
                .String("operation_id", operationId)
                .String("operation_phase", "report")
                .String("status", status)
                .String("reason", reason)
                .String("victim_id", observation.VictimId.ToString())
                .String("killer_id", observation.KillerId.ToString()));
    }

    private static void TrySendDeathReset(ZRpc serverRpc)
    {
        if (KillAttributionRpcAttempt.TrySend(
                serverRpc.IsConnected(),
                () => serverRpc.Invoke(KillAttributionProtocol.ChainResetRpc),
                out string failure))
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("PlayerCombat", "kill_chain_reset_requested")
                    .String("reason", "death"));
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_chain_reset_not_sent")
                .String("reason", "death")
                .String("failure", failure));
    }

    private static void OnChainResetAcknowledged(ZRpc rpc)
    {
        if (!ReferenceEquals(rpc, ZNet.instance?.GetServerRPC()))
        {
            EmitDeliveryRejected("chain_reset_ack_non_server_sender");
            return;
        }

        if (!deathResetPending)
        {
            EmitDeliveryRejected("chain_reset_ack_without_death");
            return;
        }

        // Valheim's reliable connection preserves the server's send order.
        // Therefore every pre-reset transition has already been observed and
        // ignored before this acknowledgment can arrive.
        deathResetPending = false;
        ChainDelivery.Reset();
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_chain_reset_acknowledged")
                .String("reason", "death"));
    }

    private static void EmitDeliveryRejected(string reason)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "confirmed_kill_delivery_rejected")
                .String("operation_phase", "delivery")
                .String("reason", reason));
    }
}
