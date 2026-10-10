using System;
using System.Collections.Generic;
using System.Linq;
using Benheim.Resident;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimServerSupport;

/// <summary>
/// Coordinates the multiplayer resident over authenticated peer RPCs. Native
/// tub ownership remains authoritative: the server validates and routes each
/// placement request, while the current owner alone changes the tub ZDO.
/// Encounter speech is a bounded request/reply that the server validates and
/// broadcasts once to the compatible connected cohort. This part owns shared
/// state, lifecycle hooks, update scheduling, and typed diagnostics.
/// </summary>
[HarmonyPatch]
internal static partial class ResidentServer
{
    private const double PendingTimeoutSeconds = 12d;
    private const float InteractionRange = 6f;
    private const int MaximumSpeechLength = 140;
    private const int MaximumReasonLength = 64;
    private const int MaximumRpcPackageBytes = 2048;
    private static readonly int BathtubPrefabHash = "piece_bathtub".GetStableHashCode();

    private static readonly ResidentPeerCohort<ZNetPeer> PeerCohort = new();
    private static readonly HashSet<ZNetPeer> RegisteredPeerHandlers = new();
    private static readonly Dictionary<ZDOID, PendingPlacement> PendingPlacements = new();
    private static readonly Dictionary<ZDOID, PendingEncounter> PendingEncounters = new();
    private static readonly Dictionary<ZDOID, ResidentEncounterCadence> EncounterCadences = new();
    private static ZRoutedRpc? registeredRoutedRpc;
    private static long publishedCohortRevision = -1L;
    private static bool? publishedReadiness;

    [HarmonyPatch(typeof(ZNet), "OnDestroy")]
    [HarmonyPrefix]
    private static void BeforeNetworkDestroy()
    {
        Reset();
    }

    // Server Support runs without the client feature assembly, so it installs
    // the same owner RPC handler on each native bathtub view loaded by the
    // dedicated server. The shared helper filters non-bathtub Smelters.
    [HarmonyPatch(typeof(Smelter), "Awake")]
    [HarmonyPostfix]
    private static void AfterSmelterAwake(Smelter __instance)
    {
        if (!IsServer() || !__instance)
        {
            return;
        }

        ResidentTub.Attach(__instance);
    }

    internal static void Update()
    {
        if (!IsServer())
        {
            return;
        }

        EnsureRoutedRpcRegistered();
        RefreshPeerCohort();
        double now = ZNet.instance!.GetTimeSeconds();
        ProcessPlacements(now);
        ExpireEncounters(now);
        RemoveExpiredEncounterCadences(now);
        PublishReadinessIfChanged();
    }

    internal static void Reset()
    {
        foreach (PendingPlacement pending in PendingPlacements.Values)
        {
            Emit(
                "placement_abandoned",
                pending.OperationId,
                pending.Tub,
                "network_reset",
                "placement");
        }

        foreach (PendingEncounter pending in PendingEncounters.Values)
        {
            Emit(
                "speech_cancelled",
                pending.OperationId,
                pending.Tub,
                "network_reset",
                "speech");
        }

        PeerCohort.Reset();
        RegisteredPeerHandlers.Clear();
        PendingPlacements.Clear();
        PendingEncounters.Clear();
        EncounterCadences.Clear();
        registeredRoutedRpc = null;
        publishedCohortRevision = -1L;
        publishedReadiness = null;
    }

    private static bool IsServer() => ZNet.instance != null && ZNet.instance.IsServer();

    private static void Emit(
        string eventName,
        string operationId,
        ZDOID tubId,
        string reason,
        string phase,
        int protocolVersion = 0)
    {
        try
        {
            DiagnosticEvent diagnostic = DiagnosticEvent.Create("GreydwarfResident", eventName)
                .String("operation_phase", phase)
                .String("reason", NormalizeReason(reason, "unspecified"));
            if (IsOperationId(operationId))
            {
                diagnostic.String("operation_id", operationId);
            }
            if (!tubId.IsNone())
            {
                diagnostic.String("tub_zdoid", tubId.ToString());
            }
            if (protocolVersion != 0)
            {
                diagnostic.Integer("protocol_version", protocolVersion);
            }

            ServerDiagnostics.Emit(diagnostic);
        }
        catch
        {
            // Diagnostics must not interrupt the native owner transaction or
            // block speech/result delivery.
        }
    }

    private sealed class PendingPlacement
    {
        internal readonly string OperationId;
        internal readonly ZDOID Tub;
        internal readonly ZNetPeer RequesterPeer;
        internal readonly long RequesterUid;
        internal readonly ZDOID RequesterCharacter;
        internal readonly long OwnerUid;
        internal readonly bool Desired;
        internal readonly int ExpectedGeneration;
        internal readonly double ExpiresAt;
        internal readonly ResidentPlacementConfirmation Confirmation;
        internal bool RequesterDisconnected;

        internal PendingPlacement(
            string operationId,
            ZDOID tub,
            ZNetPeer requesterPeer,
            long requesterUid,
            ZDOID requesterCharacter,
            long ownerUid,
            bool desired,
            int expectedGeneration,
            double expiresAt)
        {
            OperationId = operationId;
            Tub = tub;
            RequesterPeer = requesterPeer;
            RequesterUid = requesterUid;
            RequesterCharacter = requesterCharacter;
            OwnerUid = ownerUid;
            Desired = desired;
            ExpectedGeneration = expectedGeneration;
            ExpiresAt = expiresAt;
            Confirmation = new ResidentPlacementConfirmation(
                expectedGeneration,
                desired,
                expiresAt,
                PendingTimeoutSeconds);
        }
    }

    private sealed class PendingEncounter
    {
        internal readonly string OperationId;
        internal readonly ZDOID Tub;
        internal readonly int Generation;
        internal readonly ZNetPeer RequesterPeer;
        internal readonly long RequesterUid;
        internal readonly ZDOID RequesterCharacter;
        internal readonly long CohortRevision;
        internal readonly double ExpiresAt;

        internal PendingEncounter(
            string operationId,
            ZDOID tub,
            int generation,
            ZNetPeer requesterPeer,
            long requesterUid,
            ZDOID requesterCharacter,
            long cohortRevision,
            double expiresAt)
        {
            OperationId = operationId;
            Tub = tub;
            Generation = generation;
            RequesterPeer = requesterPeer;
            RequesterUid = requesterUid;
            RequesterCharacter = requesterCharacter;
            CohortRevision = cohortRevision;
            ExpiresAt = expiresAt;
        }
    }
}
