using System;
using System.Linq;
using Benheim.Resident;

namespace BenheimServerSupport;

// Placement authorization and confirmation across owner RPC and ZDO replication.
internal static partial class ResidentServer
{

    private static void OnPlacementRequest(ZNetPeer peer, ZRpc rpc, ZPackage package)
    {
        if (!TryReadPlacementRequest(package, out string operationId, out ZDOID tubId,
                out bool desired, out int expectedGeneration))
        {
            RejectPlacementRequest(peer, "invalid", ZDOID.None, "invalid_payload", 0);
            return;
        }

        if (!IsCurrentPeer(peer, rpc))
        {
            RejectPlacementRequest(peer, operationId, tubId, "requester_not_ready", expectedGeneration);
            return;
        }

        if (!AllPeersReady())
        {
            RejectPlacementRequest(peer, operationId, tubId, "peer_protocol_unavailable", expectedGeneration);
            return;
        }

        if (!TryResolveRequester(peer, out ZDO character))
        {
            RejectPlacementRequest(peer, operationId, tubId, "requester_character_unavailable", expectedGeneration);
            return;
        }

        if (!TryResolveTub(tubId, out ZDO tub))
        {
            RejectPlacementRequest(peer, operationId, tubId, "native_tub_unavailable", expectedGeneration);
            return;
        }

        int currentGeneration = ResidentTub.Generation(tub);
        if (!WithinRange(character.GetPosition(), tub.GetPosition()))
        {
            RejectPlacementRequest(peer, operationId, tubId, "requester_out_of_range", currentGeneration);
            return;
        }

        if (expectedGeneration < 0 || expectedGeneration == int.MaxValue)
        {
            RejectPlacementRequest(peer, operationId, tubId, "generation_invalid", currentGeneration);
            return;
        }

        if (expectedGeneration != currentGeneration)
        {
            RejectPlacementRequest(peer, operationId, tubId, "generation_conflict", currentGeneration);
            return;
        }

        bool currentlyInvited = ResidentTub.IsInvited(tub);
        if (currentlyInvited == desired)
        {
            RejectOrConfirmNoOp(peer, operationId, tubId, desired, currentGeneration);
            if (!desired)
            {
                CancelEncounter(tubId, "resident_dismissed");
                EncounterCooldowns.Remove(tubId);
            }
            return;
        }

        if (PendingPlacements.ContainsKey(tubId))
        {
            RejectPlacementRequest(peer, operationId, tubId, "placement_in_progress", currentGeneration);
            return;
        }

        long ownerUid = tub.GetOwner();
        if (ownerUid == 0L)
        {
            RejectPlacementRequest(peer, operationId, tubId, "tub_owner_unavailable", currentGeneration);
            return;
        }

        double now = ZNet.instance!.GetTimeSeconds();
        PendingPlacement pending = new(
            operationId,
            tubId,
            peer,
            peer.m_uid,
            character.m_uid,
            ownerUid,
            desired,
            expectedGeneration,
            now + PendingTimeoutSeconds);
        PendingPlacements.Add(tubId, pending);
        Emit("placement_requested", operationId, tubId, desired ? "invite" : "dismiss", "placement");

        try
        {
            EnsureRoutedRpcRegistered();
            ZRoutedRpc routedRpc = ZRoutedRpc.instance!;
            // This overload includes the native tub identity so the routed
            // call reaches the ZNetView handler installed on that object.
            routedRpc.InvokeRoutedRPC(
                ownerUid,
                tubId,
                ResidentProtocol.OwnerPlacementRpc,
                ZNet.GetUID(),
                operationId,
                desired,
                expectedGeneration,
                character.m_uid,
                pending.ExpiresAt);
            Emit("placement_routed", operationId, tubId, "current_owner", "placement");
        }
        catch (Exception exception)
        {
            PendingPlacements.Remove(tubId);
            RejectPlacement(peer, operationId, tubId, false,
                "owner_request_failed_" + exception.GetType().Name, currentGeneration);
            Emit("placement_delivery_failed", operationId, tubId,
                exception.GetType().Name, "placement");
        }
    }

    private static void RejectPlacementRequest(
        ZNetPeer peer,
        string operationId,
        ZDOID tubId,
        string reason,
        int generation)
    {
        RejectPlacement(peer, operationId, tubId, false, reason, generation);
        Emit("placement_rejected", operationId, tubId, reason, "placement");
    }

    private static void OnOwnerPlacementResult(
        long sender,
        string operationId,
        ZDOID tubId,
        bool ownerSuccess,
        string ownerReason,
        int reportedGeneration)
    {
        if (!IsServer())
        {
            return;
        }

        if (!PendingPlacements.TryGetValue(tubId, out PendingPlacement? pending) ||
            !string.Equals(pending.OperationId, operationId, StringComparison.Ordinal))
        {
            Emit("placement_owner_result_rejected", operationId, tubId,
                "unknown_operation", "owner_result");
            return;
        }

        if (sender != pending.OwnerUid)
        {
            Emit("placement_owner_result_rejected", operationId, tubId,
                "not_requested_owner", "owner_result");
            return;
        }

        if (!pending.Confirmation.TryRecordOwnerResult(
                ownerSuccess,
                NormalizeReason(ownerReason, ownerSuccess ? "applied" : "owner_rejected"),
                reportedGeneration))
        {
            Emit("placement_owner_result_rejected", operationId, tubId,
                "duplicate_owner_result", "owner_result");
            return;
        }

        ProcessPlacement(pending, ZNet.instance!.GetTimeSeconds());
    }

    private static void ProcessPlacements(double now)
    {
        foreach (PendingPlacement pending in PendingPlacements.Values.ToArray())
        {
            ProcessPlacement(pending, now);
        }
    }

    private static void ProcessPlacement(PendingPlacement pending, double now)
    {
        bool hasTub = TryResolveTub(pending.Tub, out ZDO tub);
        ResidentPlacementProgress progress = pending.Confirmation.Observe(
            now,
            hasTub,
            hasTub ? ResidentTub.Generation(tub) : pending.ExpectedGeneration,
            hasTub && ResidentTub.IsInvited(tub),
            out string reason);

        switch (progress)
        {
            case ResidentPlacementProgress.Applied:
                ZDOMan.instance?.SetDirtySector(tub);
                CompletePlacement(pending, true, pending.Desired ? "invited" : "dismissed",
                    ResidentTub.Generation(tub));
                break;
            case ResidentPlacementProgress.LateApplied:
                ZDOMan.instance?.SetDirtySector(tub);
                PendingPlacements.Remove(pending.Tub);
                if (!pending.Desired)
                {
                    CancelEncounter(pending.Tub, "resident_dismissed");
                    EncounterCooldowns.Remove(pending.Tub);
                }
                Emit("placement_late_confirmed", pending.OperationId, pending.Tub,
                    pending.Desired ? "invited" : "dismissed", "placement");
                break;
            case ResidentPlacementProgress.Rejected:
                CompletePlacement(pending, false, reason,
                    hasTub ? ResidentTub.Generation(tub) : pending.ExpectedGeneration);
                break;
            case ResidentPlacementProgress.TimedOut:
                if (!pending.RequesterDisconnected)
                {
                    SendPlacementResult(pending.RequesterPeer, pending.OperationId, pending.Tub,
                        false, reason, hasTub ? ResidentTub.Generation(tub) : pending.ExpectedGeneration);
                }
                Emit("placement_timed_out", pending.OperationId, pending.Tub,
                    reason, "placement");
                break;
            case ResidentPlacementProgress.Expired:
                PendingPlacements.Remove(pending.Tub);
                Emit("placement_late_ack_expired", pending.OperationId, pending.Tub,
                    reason, "placement");
                break;
        }
    }

    private static void CompletePlacement(
        PendingPlacement pending,
        bool success,
        string reason,
        int generation)
    {
        if (!PendingPlacements.TryGetValue(pending.Tub, out PendingPlacement? current) ||
            !ReferenceEquals(current, pending))
        {
            return;
        }

        PendingPlacements.Remove(pending.Tub);
        if (!pending.Confirmation.TimeoutReported && !pending.RequesterDisconnected)
        {
            SendPlacementResult(pending.RequesterPeer, pending.OperationId, pending.Tub,
                success, reason, generation);
        }

        if (success && !pending.Desired)
        {
            CancelEncounter(pending.Tub, "resident_dismissed");
            EncounterCooldowns.Remove(pending.Tub);
        }

        Emit(success ? "placement_applied" : "placement_rejected",
            pending.OperationId, pending.Tub, reason, "owner_result");
    }

    private static void CancelPlacementsForPeer(ZNetPeer peer, string reason)
    {
        foreach (PendingPlacement pending in PendingPlacements.Values
                     .Where(item => item.RequesterUid == peer.m_uid || item.OwnerUid == peer.m_uid)
                     .ToArray())
        {
            if (pending.RequesterUid == peer.m_uid)
            {
                pending.RequesterDisconnected = true;
            }

            // The owner RPC is already in flight and cannot be withdrawn.
            // Retain its bounded confirmation record so an applied mutation
            // can still dirty the save sector after this peer disconnects.
            Emit("placement_disconnect_observed", pending.OperationId, pending.Tub,
                reason, "placement");
        }
    }

}
