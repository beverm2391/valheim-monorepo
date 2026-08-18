using BenheimQoL.Infrastructure;
using BenheimQoL.KillAttribution;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BenheimServerSupport;

[HarmonyPatch]
internal static class KillAttributionServer
{
    // This is a transport-memory bound, not a gameplay window. A duplicate
    // report arrives immediately; retaining recent victim IDs prevents it from
    // incrementing the canonical sequence while bounding server memory.
    private const int RecentVictimCapacity = 4096;
    private static readonly int CanonicalBoarPrefabHash = "Boar".GetStableHashCode();

    private static readonly ConfirmedKillState<ZDOID, ZDOID> State =
        new ConfirmedKillState<ZDOID, ZDOID>(RecentVictimCapacity);
    private static readonly KillChainState<ZDOID> Chains =
        new KillChainState<ZDOID>();
    private static readonly List<KillChainTransition<ZDOID>> ExpiredChains =
        new List<KillChainTransition<ZDOID>>();

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
        peer.m_rpc.Register(
            KillAttributionProtocol.ChainResetRpc,
            rpc => OnChainReset(peer, rpc));
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
            Chains.RemoveKiller(peer.m_characterID);
            KillChainDeliveryRuntime.RemoveKiller(peer.m_characterID);
        }
    }

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    [HarmonyPrefix]
    private static void BeforeNetworkDestroy()
    {
        Reset();
    }

    internal static void Update()
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        double serverTimeSeconds = ZNet.instance.GetTimeSeconds();
        KillChainDeliveryRuntime.Update(serverTimeSeconds, Chains);
        ExpireDueChains(serverTimeSeconds);
    }

    private static void ExpireDueChains(double serverTimeSeconds)
    {
        Chains.CollectExpired(serverTimeSeconds, ExpiredChains);
        for (int index = 0; index < ExpiredChains.Count; index++)
        {
            KillChainDeliveryRuntime.Deliver(
                ExpiredChains[index],
                "inactivity",
                Chains);
        }
    }

    internal static void Reset()
    {
        State.Reset();
        Chains.Reset();
        ExpiredChains.Clear();
        KillChainDeliveryRuntime.Reset();
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

    private static void OnChainReset(ZNetPeer reporter, ZRpc rpc)
    {
        if (ZNet.instance == null
            || !ZNet.instance.IsServer()
            || !ReferenceEquals(rpc, reporter.m_rpc)
            || !reporter.IsReady()
            || reporter.m_socket == null
            || reporter.m_characterID.IsNone())
        {
            EmitRejected("unauthenticated_chain_reset", string.Empty, ZDOID.None, ZDOID.None);
            return;
        }

        double serverTimeSeconds = ZNet.instance.GetTimeSeconds();
        if (Chains.ResetKiller(
                reporter.m_characterID,
                serverTimeSeconds,
                out KillChainTransition<ZDOID> transition))
        {
            KillChainDeliveryRuntime.Deliver(reporter, transition, "death", Chains);
        }
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

        bool isBoss = characterPrefab != null && characterPrefab.IsBoss();
        bool isTamed = victim.GetBool(ZDOVars.s_tamed, false);
        bool hasMonsterAi = prefab != null && prefab.GetComponent<MonsterAI>() != null;
        bool isCanonicalBoar = prefabHash == CanonicalBoarPrefabHash;
        bool qualifiesForChain = characterPrefab != null
            && VictimQualification.IsHostileCreature(
                characterPrefab.GetFaction(),
                isBoss,
                isTamed,
                hasMonsterAi,
                isCanonicalBoar);
        string qualification = characterPrefab == null
            ? "prefab_character_missing"
            : isTamed
                ? "tamed"
                : isCanonicalBoar
                    ? "passive_boar"
                    : !hasMonsterAi && !isBoss
                        ? "non_monster_ai"
                        : qualifiesForChain
                            ? "hostile_creature"
                            : "non_hostile_faction";
        double serverTimeSeconds = ZNet.instance.GetTimeSeconds();

        ConfirmedKillMessage confirmation = new ConfirmedKillMessage(
            report.OperationId,
            report.VictimId,
            report.KillerId,
            prefabHash,
            prefabName,
            Math.Max(1, victim.GetInt(ZDOVars.s_level, 1)),
            isBoss,
            isTamed,
            victim.GetPosition(),
            sequence,
            serverTimeSeconds);

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
                .Boolean("victim_monster_ai", hasMonsterAi)
                .Boolean("victim_canonical_boar", isCanonicalBoar)
                .String(
                    "victim_faction",
                    characterPrefab == null
                        ? "unknown"
                        : characterPrefab.GetFaction().ToString())
                .Boolean("chain_qualifies", qualifiesForChain)
                .String("chain_qualification", qualification)
                .Integer("server_sequence", sequence)
                .Number("server_time_seconds", confirmation.ServerTimeSeconds));

        if (qualifiesForChain)
        {
            ExpireDueChains(serverTimeSeconds);
            KillChainTransition<ZDOID> transition = Chains.Advance(
                report.KillerId,
                sequence,
                serverTimeSeconds);
            KillChainDeliveryRuntime.Deliver(
                killerPeer,
                transition,
                "qualifying_kill",
                Chains);
        }
    }

    internal static ZNetPeer? FindReadyPeer(ZDOID characterId)
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
