using BenheimQoL.Infrastructure;
using BenheimQoL.PlayerCombat;
using HarmonyLib;
using System;

namespace BenheimQoL.KillAttribution;

[HarmonyPatch]
internal static class KillAttributionClient
{
    private static ZRpc? compatibleServerRpc;
    private static readonly KillChainDeliveryCursor ChainDelivery =
        new KillChainDeliveryCursor();

    internal static bool HasCompatibleServer =>
        compatibleServerRpc != null
        && ReferenceEquals(compatibleServerRpc, ZNet.instance?.GetServerRPC());

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    [HarmonyPostfix]
    private static void AfterNewConnection(ZNetPeer peer)
    {
        if (ZNet.instance == null || ZNet.instance.IsServer() || !peer.m_server)
        {
            return;
        }

        compatibleServerRpc = null;
        ChainDelivery.Reset();
        peer.m_rpc.Register<int>(KillAttributionProtocol.CapabilityRpc, OnCapability);
        peer.m_rpc.Register<ZPackage>(KillAttributionProtocol.ConfirmedRpc, OnConfirmed);
        peer.m_rpc.Register<ZPackage>(
            KillAttributionProtocol.ChainTransitionRpc,
            OnChainTransition);
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    [HarmonyPrefix]
    private static void BeforeNetworkDestroy()
    {
        compatibleServerRpc = null;
        ChainDelivery.Reset();
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

        try
        {
            serverRpc.Invoke(
                KillAttributionProtocol.ReportRpc,
                KillAttributionProtocol.BuildReport(
                    operationId,
                    observation.VictimId,
                    observation.KillerId));
            EmitReport(operationId, observation, "sent", "owner_lethal_transition");
        }
        catch (Exception exception)
        {
            EmitReport(
                operationId,
                observation,
                "not_sent",
                $"send_failed_{exception.GetType().Name}");
        }
    }

    internal static void ReportLocalDeath(Player player)
    {
        if (player != Player.m_localPlayer
            || ZNet.instance == null
            || ZNet.instance.IsServer())
        {
            return;
        }

        ZRpc? serverRpc = ZNet.instance.GetServerRPC();
        if (serverRpc == null || !HasCompatibleServer)
        {
            return;
        }

        try
        {
            serverRpc.Invoke(KillAttributionProtocol.ChainResetRpc);
            Diagnostics.Emit(
                DiagnosticEvent.Create("PlayerCombat", "kill_chain_reset_requested")
                    .String("reason", "death"));
        }
        catch (Exception exception)
        {
            Diagnostics.Emit(
                DiagnosticEvent.Create("PlayerCombat", "kill_chain_reset_not_sent")
                    .String("reason", "death")
                    .String("failure", exception.GetType().Name));
        }
    }

    private static void OnCapability(ZRpc rpc, int version)
    {
        if (!ReferenceEquals(rpc, ZNet.instance?.GetServerRPC()))
        {
            return;
        }

        compatibleServerRpc = version == KillAttributionProtocol.Version ? rpc : null;
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_feed_capability")
                .Integer("protocol_version", version)
                .Boolean("compatible", compatibleServerRpc != null));
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
            KillChainTransitionKind.Reset => BerserkerChainTransitionKind.Reset,
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

    private static void EmitDeliveryRejected(string reason)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "confirmed_kill_delivery_rejected")
                .String("operation_phase", "delivery")
                .String("reason", reason));
    }
}
