using System;
using System.Collections.Generic;
using System.Linq;
using Benheim.Resident;
using HarmonyLib;

namespace BenheimServerSupport;

// Authenticated peer lifecycle and capability readiness for the full cohort.
internal static partial class ResidentServer
{

    [HarmonyPatch(typeof(ZNet), "OnNewConnection")]
    [HarmonyPostfix]
    private static void AfterNewConnection(ZNetPeer peer)
    {
        if (!IsServer() || peer?.m_rpc == null)
        {
            return;
        }

        TrackPeer(peer);
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect), typeof(ZNetPeer))]
    [HarmonyPostfix]
    private static void AfterDisconnect(ZNetPeer peer)
    {
        if (!IsServer() || peer == null)
        {
            return;
        }

        bool changed = PeerCohort.Remove(peer);
        RegisteredPeerHandlers.Remove(peer);
        CancelEncountersForPeer(peer, "peer_disconnected");
        CancelPlacementsForPeer(peer, "peer_disconnected");
        if (changed)
        {
            CancelAllEncounters("peer_cohort_changed");
            PublishReadinessIfChanged();
        }
    }

    private static void OnCapability(ZNetPeer peer, ZRpc rpc, int version)
    {
        if (!IsCurrentPeer(peer, rpc, requireReady: false))
        {
            Emit("capability_rejected", string.Empty, ZDOID.None, "non_current_peer", "capability");
            return;
        }

        if (!PeerCohort.TryRecordVersion(peer, version, out int? priorVersion))
        {
            Emit("capability_rejected", string.Empty, ZDOID.None, "peer_not_tracked", "capability");
            return;
        }

        if (priorVersion != version)
        {
            CancelAllEncounters("peer_cohort_changed");
            Emit(
                "capability_received",
                string.Empty,
                ZDOID.None,
                version == ResidentProtocol.Version ? "matching_version" : "incompatible_version",
                "capability",
                version);
        }

        // Clients ask periodically while joining. Answer every heartbeat so a
        // lost response repairs itself, but only broadcast when cohort state
        // changed; otherwise one client would make all peers chat every second.
        TrySendCapability(rpc, AllPeersReady());
        PublishReadinessIfChanged(peer);
    }

    private static void EnsureRoutedRpcRegistered()
    {
        ZRoutedRpc? rpc = ZRoutedRpc.instance;
        if (rpc == null || ReferenceEquals(registeredRoutedRpc, rpc))
        {
            return;
        }

        if (registeredRoutedRpc != null)
        {
            Reset();
        }

        rpc.Register<string, ZDOID, bool, string, int>(
            ResidentProtocol.OwnerPlacementResultRpc,
            OnOwnerPlacementResult);
        registeredRoutedRpc = rpc;
        Emit("rpc_registered", string.Empty, ZDOID.None, "owner_result", "registration");
    }

    private static void TrackPeer(ZNetPeer peer, bool publish = true)
    {
        if (peer?.m_rpc == null)
        {
            return;
        }

        bool cohortChanged = PeerCohort.Track(peer);
        if (cohortChanged)
        {
            CancelAllEncounters("peer_cohort_changed");
        }

        if (RegisteredPeerHandlers.Add(peer))
        {
            // Capture the authenticated connection in each direct callback.
            // A client cannot supply a peer ID to impersonate another user.
            peer.m_rpc.Register<int>(
                ResidentProtocol.CapabilityRpc,
                (rpc, version) => OnCapability(peer, rpc, version));
            peer.m_rpc.Register<ZPackage>(
                ResidentProtocol.PlacementRequestRpc,
                (rpc, package) => OnPlacementRequest(peer, rpc, package));
            peer.m_rpc.Register<ZPackage>(
                ResidentProtocol.EncounterRequestRpc,
                (rpc, package) => OnEncounterRequest(peer, rpc, package));
            peer.m_rpc.Register<ZPackage>(
                ResidentProtocol.SpeechReplyRpc,
                (rpc, package) => OnSpeechReply(peer, rpc, package));
        }

        if (publish && cohortChanged)
        {
            PublishReadinessIfChanged();
        }
    }

    private static void RefreshPeerCohort()
    {
        ZNet? network = ZNet.instance;
        if (network == null)
        {
            return;
        }

        List<ZNetPeer> peers = network.GetPeers();
        foreach (ZNetPeer peer in peers)
        {
            TrackPeer(peer, publish: false);
        }

        bool changed = false;
        foreach (ZNetPeer stale in PeerCohort.TrackedPeers.Where(peer => !peers.Contains(peer)).ToArray())
        {
            PeerCohort.Remove(stale);
            RegisteredPeerHandlers.Remove(stale);
            CancelEncountersForPeer(stale, "peer_disconnected");
            CancelPlacementsForPeer(stale, "peer_disconnected");
            changed = true;
        }

        if (changed)
        {
            CancelAllEncounters("peer_cohort_changed");
        }
    }

    private static bool AllPeersReady()
    {
        if (!IsServer() || ZNet.instance == null)
        {
            return false;
        }

        foreach (ZNetPeer peer in ZNet.instance.GetPeers())
        {
            if (!PeerCohort.TryGetVersion(peer, out int? version) ||
                version != ResidentProtocol.Version ||
                !peer.IsReady() || peer.m_socket == null || peer.m_rpc == null || !peer.m_rpc.IsConnected())
            {
                return false;
            }
        }

        return true;
    }

    private static void PublishReadinessIfChanged(ZNetPeer? excludePeer = null)
    {
        if (!IsServer() || ZNet.instance == null)
        {
            return;
        }

        bool allReady = AllPeersReady();
        bool changed = publishedCohortRevision != PeerCohort.Revision ||
            publishedReadiness != allReady;
        if (!changed)
        {
            return;
        }

        publishedCohortRevision = PeerCohort.Revision;
        publishedReadiness = allReady;
        foreach (ZNetPeer peer in ZNet.instance.GetPeers())
        {
            // A capability request proves that this peer registered its response
            // handler. Do not send to peers that have not announced support yet.
            if (!PeerCohort.TryGetVersion(peer, out int? version) || !version.HasValue)
            {
                continue;
            }

            if (ReferenceEquals(peer, excludePeer))
            {
                continue;
            }

            TrySendCapability(peer.m_rpc, allReady);
        }

        Emit(
            "cohort_readiness",
            string.Empty,
            ZDOID.None,
            allReady ? "all_peers_ready" : "peer_support_missing",
            "capability",
            ResidentProtocol.Version);
    }

    private static bool TrySendCapability(ZRpc rpc, bool allReady)
    {
        try
        {
            if (!rpc.IsConnected())
            {
                return false;
            }

            rpc.Invoke(ResidentProtocol.CapabilityRpc, ResidentProtocol.Version, allReady);
            return true;
        }
        catch (Exception exception)
        {
            Emit("capability_delivery_failed", string.Empty, ZDOID.None,
                exception.GetType().Name, "capability");
            return false;
        }
    }

    private static bool IsCurrentPeer(ZNetPeer peer, ZRpc rpc, bool requireReady = true)
    {
        return IsServer() && peer != null && rpc != null &&
            ReferenceEquals(rpc, peer.m_rpc) &&
            PeerCohort.TryGetVersion(peer, out _) &&
            (!requireReady || peer.IsReady()) &&
            peer.m_socket != null && rpc.IsConnected() &&
            ZNet.instance!.GetPeers().Contains(peer);
    }

    private static bool TryFindPeer(long uid, out ZNetPeer peer)
    {
        peer = null!;
        if (ZNet.instance == null)
        {
            return false;
        }

        foreach (ZNetPeer candidate in ZNet.instance.GetPeers())
        {
            if (candidate.m_uid == uid)
            {
                peer = candidate;
                return candidate.IsReady() && candidate.m_socket != null &&
                    candidate.m_rpc.IsConnected();
            }
        }

        return false;
    }

}
