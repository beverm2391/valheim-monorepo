using BenheimQoL.Infrastructure;
using BenheimQoL.KillAttribution;
using HarmonyLib;
using System;
using UnityEngine;

namespace BenheimServerSupport;

[HarmonyPatch]
internal static class KillAttributionServer
{
    // This is a transport-memory bound, not a gameplay window. A duplicate
    // report arrives immediately; retaining recent victim IDs prevents it from
    // incrementing the canonical sequence while bounding server memory.
    private const int RecentVictimCapacity = 4096;

    private static readonly ConfirmedKillState<ZDOID, ZDOID> State =
        new ConfirmedKillState<ZDOID, ZDOID>(RecentVictimCapacity);

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    [HarmonyPostfix]
    private static void AfterNewConnection(ZNetPeer peer)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        // The captured peer binds every report to its authenticated direct
        // connection. The client cannot select a different reporting sender.
        peer.m_rpc.Register<ZPackage>(
            KillAttributionProtocol.ReportRpc,
            (rpc, package) => OnReport(peer, rpc, package));
        peer.m_rpc.Invoke(
            KillAttributionProtocol.CapabilityRpc,
            KillAttributionProtocol.Version);
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect), typeof(ZNetPeer))]
    [HarmonyPrefix]
    private static void BeforeDisconnect(ZNetPeer peer)
    {
        if (!peer.m_characterID.IsNone())
        {
            State.RemoveKiller(peer.m_characterID);
        }
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    [HarmonyPrefix]
    private static void BeforeNetworkDestroy()
    {
        State.Reset();
    }

    internal static void ConfirmServerOwned(LethalHitObservation observation)
    {
        if (!observation.Eligible
            || ZNet.instance == null
            || !ZNet.instance.IsServer())
        {
            return;
        }

        ZDO? victim = ZDOMan.instance?.GetZDO(observation.VictimId);
        if (victim == null || victim.GetOwner() != ZNet.GetUID())
        {
            EmitRejected("server_owner_mismatch", string.Empty, observation.VictimId, observation.KillerId);
            return;
        }

        Confirm(
            new KillReport(
                Guid.NewGuid().ToString("N"),
                observation.VictimId,
                observation.KillerId),
            victim);
    }

    private static void OnReport(ZNetPeer reporter, ZRpc rpc, ZPackage package)
    {
        if (ZNet.instance == null
            || !ZNet.instance.IsServer()
            || !ReferenceEquals(rpc, reporter.m_rpc)
            || !reporter.IsReady()
            || reporter.m_socket == null)
        {
            EmitRejected("unauthenticated_reporter", string.Empty, ZDOID.None, ZDOID.None);
            return;
        }

        if (!KillAttributionProtocol.TryReadReport(package, out KillReport report))
        {
            EmitRejected("invalid_payload", string.Empty, ZDOID.None, ZDOID.None);
            return;
        }

        ZDO? victim = ZDOMan.instance?.GetZDO(report.VictimId);
        if (victim == null)
        {
            EmitRejected("victim_missing", report.OperationId, report.VictimId, report.KillerId);
            return;
        }

        if (victim.GetOwner() != reporter.m_uid)
        {
            EmitRejected("reporter_not_victim_owner", report.OperationId, report.VictimId, report.KillerId);
            return;
        }

        Confirm(report, victim);
    }

    private static void Confirm(KillReport report, ZDO victim)
    {
        if (victim.GetLong(ZDOVars.s_playerID, 0L) != 0L)
        {
            EmitRejected("victim_is_player", report.OperationId, report.VictimId, report.KillerId);
            return;
        }

        ZNetPeer? killerPeer = FindReadyPeer(report.KillerId);
        if (killerPeer == null)
        {
            EmitRejected("killer_not_connected", report.OperationId, report.VictimId, report.KillerId);
            return;
        }

        if (!State.TryConfirm(report.VictimId, report.KillerId, out long sequence))
        {
            EmitRejected("duplicate_victim", report.OperationId, report.VictimId, report.KillerId);
            return;
        }

        int prefabHash = victim.GetPrefab();
        GameObject? prefab = ZNetScene.instance?.GetPrefab(prefabHash);
        Character? characterPrefab = prefab?.GetComponent<Character>();
        string prefabName = prefab != null && !string.IsNullOrEmpty(prefab.name)
            ? prefab.name
            : $"prefab_{prefabHash}";
        if (prefabName.Length > 128)
        {
            prefabName = prefabName.Substring(0, 128);
        }

        ConfirmedKillMessage confirmation = new ConfirmedKillMessage(
            report.OperationId,
            report.VictimId,
            report.KillerId,
            prefabHash,
            prefabName,
            Math.Max(1, victim.GetInt(ZDOVars.s_level, 1)),
            characterPrefab != null && characterPrefab.IsBoss(),
            victim.GetBool(ZDOVars.s_tamed, false),
            victim.GetPosition(),
            sequence,
            ZNet.instance.GetTimeSeconds());

        try
        {
            killerPeer.m_rpc.Invoke(
                KillAttributionProtocol.ConfirmedRpc,
                KillAttributionProtocol.BuildConfirmation(confirmation));
        }
        catch (Exception exception)
        {
            State.ReleaseFailedDelivery(report.VictimId);
            EmitRejected(
                $"delivery_failed_{exception.GetType().Name}",
                report.OperationId,
                report.VictimId,
                report.KillerId);
            return;
        }

        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "confirmed_kill")
                .String("operation_id", report.OperationId)
                .String("operation_phase", "confirmed")
                .String("status", "confirmed")
                .String("victim_id", report.VictimId.ToString())
                .String("killer_id", report.KillerId.ToString())
                .Integer("killer_peer", killerPeer.m_uid)
                .String("victim_prefab", prefabName)
                .Integer("victim_prefab_hash", prefabHash)
                .Integer("victim_level", confirmation.VictimLevel)
                .Boolean("victim_boss", confirmation.VictimIsBoss)
                .Boolean("victim_tamed", confirmation.VictimIsTamed)
                .Integer("server_sequence", sequence)
                .Number("server_time_seconds", confirmation.ServerTimeSeconds));
    }

    private static ZNetPeer? FindReadyPeer(ZDOID characterId)
    {
        if (ZNet.instance == null)
        {
            return null;
        }

        foreach (ZNetPeer peer in ZNet.instance.GetPeers())
        {
            if (peer.IsReady()
                && peer.m_socket != null
                && peer.m_characterID == characterId)
            {
                return peer;
            }
        }

        return null;
    }

    private static void EmitRejected(
        string reason,
        string operationId,
        ZDOID victimId,
        ZDOID killerId)
    {
        ServerDiagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_report_rejected")
                .String("operation_id", operationId)
                .String("operation_phase", "validation")
                .String("status", "rejected")
                .String("reason", reason)
                .String("victim_id", victimId.IsNone() ? string.Empty : victimId.ToString())
                .String("killer_id", killerId.IsNone() ? string.Empty : killerId.ToString()));
    }
}
