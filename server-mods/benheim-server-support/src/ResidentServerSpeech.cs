using System;
using System.Linq;
using Benheim.Resident;

namespace BenheimServerSupport;

// Bounded encounter requests, validated replies, cancellation, and broadcast.
internal static partial class ResidentServer
{

    private static void OnEncounterRequest(ZNetPeer peer, ZRpc rpc, ZPackage package)
    {
        if (!TryReadEncounterRequest(package, out string operationId, out ZDOID tubId,
                out int generation))
        {
            RejectEncounter(peer, "invalid", ZDOID.None, 0, "invalid_payload");
            Emit("speech_request_rejected", "invalid", ZDOID.None, "invalid_payload", "encounter");
            return;
        }

        if (!IsCurrentPeer(peer, rpc))
        {
            RejectEncounter(peer, operationId, tubId, generation, "requester_not_ready");
            Emit("speech_request_rejected", operationId, tubId, "requester_not_ready", "encounter");
            return;
        }

        if (!AllPeersReady())
        {
            RejectEncounter(peer, operationId, tubId, generation, "peer_protocol_unavailable");
            Emit("speech_request_rejected", operationId, tubId, "peer_protocol_unavailable", "encounter");
            return;
        }

        if (!TryResolveRequester(peer, out ZDO character))
        {
            RejectEncounter(peer, operationId, tubId, generation, "requester_character_unavailable");
            Emit("speech_request_rejected", operationId, tubId,
                "requester_character_unavailable", "encounter");
            return;
        }

        if (!TryResolveTub(tubId, out ZDO tub) || !ResidentTub.IsInvited(tub))
        {
            RejectEncounter(peer, operationId, tubId, generation, "resident_unavailable");
            Emit("speech_request_rejected", operationId, tubId, "resident_unavailable", "encounter");
            return;
        }

        int currentGeneration = ResidentTub.Generation(tub);
        if (generation != currentGeneration)
        {
            RejectEncounter(peer, operationId, tubId, currentGeneration, "generation_conflict");
            Emit("speech_request_rejected", operationId, tubId, "generation_conflict", "encounter");
            return;
        }

        if (!WithinRange(character.GetPosition(), tub.GetPosition()))
        {
            RejectEncounter(peer, operationId, tubId, currentGeneration, "requester_out_of_range");
            Emit("speech_request_rejected", operationId, tubId, "requester_out_of_range", "encounter");
            return;
        }

        double now = ZNet.instance!.GetTimeSeconds();
        if (PendingEncounters.TryGetValue(tubId, out PendingEncounter? existing) &&
            now < existing.ExpiresAt)
        {
            RejectEncounter(peer, operationId, tubId, currentGeneration, "encounter_in_progress");
            Emit("speech_request_rejected", operationId, tubId, "encounter_in_progress", "encounter");
            return;
        }

        if (EncounterCooldowns.TryGetValue(tubId, out double cooldownUntil) && now < cooldownUntil)
        {
            RejectEncounter(peer, operationId, tubId, currentGeneration, "resident_cooldown");
            Emit("speech_request_rejected", operationId, tubId, "resident_cooldown", "encounter");
            return;
        }

        PendingEncounter pending = new(
            operationId,
            tubId,
            generation,
            peer,
            peer.m_uid,
            character.m_uid,
            PeerCohort.Revision,
            now + PendingTimeoutSeconds);
        PendingEncounters[tubId] = pending;
        EncounterCooldowns[tubId] = now + EncounterCooldownSeconds;
        Emit("speech_request_granted", operationId, tubId, "approach", "encounter");
        SendEncounterGrant(peer, operationId, tubId, generation, character.m_uid, true, "granted");
    }

    private static void OnSpeechReply(ZNetPeer peer, ZRpc rpc, ZPackage package)
    {
        if (!TryReadSpeechReply(package, out string operationId, out ZDOID tubId,
                out int generation, out bool speak, out string text, out string replyReason))
        {
            Emit("speech_reply_rejected", "invalid", ZDOID.None, "invalid_payload", "speech_reply");
            return;
        }

        if (!IsCurrentPeer(peer, rpc))
        {
            Emit("speech_reply_rejected", operationId, tubId, "requester_not_ready", "speech_reply");
            return;
        }

        if (!PendingEncounters.TryGetValue(tubId, out PendingEncounter? pending) ||
            !string.Equals(pending.OperationId, operationId, StringComparison.Ordinal) ||
            pending.RequesterUid != peer.m_uid)
        {
            Emit("speech_reply_rejected", operationId, tubId, "unknown_operation", "speech_reply");
            return;
        }

        double now = ZNet.instance!.GetTimeSeconds();
        if (now >= pending.ExpiresAt)
        {
            CompleteEncounter(pending, false, string.Empty, "server_timeout");
            return;
        }

        if (pending.Generation != generation || pending.CohortRevision != PeerCohort.Revision || !AllPeersReady())
        {
            CancelEncounter(tubId, "peer_cohort_changed");
            Emit("speech_reply_rejected", operationId, tubId, "peer_cohort_changed", "speech_reply");
            return;
        }

        if (!TryResolveRequester(peer, out ZDO character) ||
            character.m_uid != pending.RequesterCharacter ||
            !WithinRange(character.GetPosition(), GetTubPosition(tubId)))
        {
            CancelEncounter(tubId, "visitor_unavailable");
            Emit("speech_reply_rejected", operationId, tubId, "visitor_unavailable", "speech_reply");
            return;
        }

        if (!TryResolveTub(tubId, out ZDO tub) || !ResidentTub.IsInvited(tub) ||
            ResidentTub.Generation(tub) != generation)
        {
            CancelEncounter(tubId, "resident_changed");
            Emit("speech_reply_rejected", operationId, tubId, "resident_changed", "speech_reply");
            return;
        }

        if (speak && !IsPlainSpeech(text))
        {
            speak = false;
            text = string.Empty;
            replyReason = "invalid_model_text";
        }
        else if (!speak)
        {
            text = string.Empty;
        }

        CompleteEncounter(pending, speak, text, NormalizeReason(replyReason, "model_silence"));
    }

    private static void ExpireEncounters(double now)
    {
        foreach (PendingEncounter pending in PendingEncounters.Values.ToArray())
        {
            if (now >= pending.ExpiresAt)
            {
                CompleteEncounter(pending, false, string.Empty, "server_timeout");
                continue;
            }

            if (pending.CohortRevision != PeerCohort.Revision || !AllPeersReady())
            {
                CancelEncounter(pending.Tub, "peer_cohort_changed");
                continue;
            }

            if (!TryFindPeer(pending.RequesterUid, out ZNetPeer peer) ||
                !TryResolveRequester(peer, out ZDO character) ||
                character.m_uid != pending.RequesterCharacter ||
                !TryResolveTub(pending.Tub, out ZDO tub) ||
                !ResidentTub.IsInvited(tub) ||
                ResidentTub.Generation(tub) != pending.Generation ||
                !WithinRange(character.GetPosition(), tub.GetPosition()))
            {
                CancelEncounter(pending.Tub, "visitor_or_resident_changed");
            }
        }
    }

    private static void RemoveExpiredCooldowns(double now)
    {
        foreach (ZDOID tub in EncounterCooldowns
                     .Where(pair => now >= pair.Value)
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            EncounterCooldowns.Remove(tub);
        }
    }

    private static void CompleteEncounter(PendingEncounter pending, bool speak, string text, string reason)
    {
        if (!PendingEncounters.TryGetValue(pending.Tub, out PendingEncounter? current) ||
            !ReferenceEquals(current, pending))
        {
            return;
        }

        PendingEncounters.Remove(pending.Tub);
        if (!AllPeersReady() || pending.CohortRevision != PeerCohort.Revision)
        {
            Emit("speech_cancelled", pending.OperationId, pending.Tub,
                "peer_cohort_changed", "speech");
            return;
        }

        reason = NormalizeReason(reason, speak ? "model_speech" : "model_silence");
        string safeText = speak ? text : string.Empty;
        if (speak && !IsPlainSpeech(safeText))
        {
            speak = false;
            safeText = string.Empty;
            reason = "invalid_model_text";
        }

        ZPackage result = new();
        result.Write(pending.OperationId);
        result.Write(pending.Tub);
        result.Write(pending.Generation);
        result.Write(pending.RequesterCharacter);
        result.Write(speak);
        result.Write(safeText);
        result.Write(reason);

        foreach (ZNetPeer peer in ZNet.instance!.GetPeers())
        {
            try
            {
                if (PeerCohort.TryGetVersion(peer, out int? version) &&
                    version == ResidentProtocol.Version && peer.IsReady() &&
                    peer.m_socket != null && peer.m_rpc.IsConnected())
                {
                    peer.m_rpc.Invoke(ResidentProtocol.SpeechResultRpc, new ZPackage(result.GetArray()));
                }
            }
            catch (Exception exception)
            {
                Emit("speech_broadcast_failed", pending.OperationId, pending.Tub,
                    exception.GetType().Name, "speech_result");
            }
        }

        Emit(speak ? "speech_broadcast" : "speech_silence", pending.OperationId,
            pending.Tub, reason, "speech_result");
    }

    private static void CancelEncounter(ZDOID tub, string reason)
    {
        if (!PendingEncounters.TryGetValue(tub, out PendingEncounter? pending))
        {
            return;
        }

        PendingEncounters.Remove(tub);
        Emit("speech_cancelled", pending.OperationId, tub, reason, "speech");
    }

    private static void CancelAllEncounters(string reason)
    {
        foreach (ZDOID tub in PendingEncounters.Keys.ToArray())
        {
            CancelEncounter(tub, reason);
        }
    }

    private static void CancelEncountersForPeer(ZNetPeer peer, string reason)
    {
        foreach (PendingEncounter pending in PendingEncounters.Values
                     .Where(item => item.RequesterUid == peer.m_uid).ToArray())
        {
            CancelEncounter(pending.Tub, reason);
        }
    }

    private static void RejectEncounter(
        ZNetPeer peer,
        string operationId,
        ZDOID tubId,
        int generation,
        string reason)
    {
        SendEncounterGrant(peer, operationId, tubId, generation,
            TryResolveRequester(peer, out ZDO character) ? character.m_uid : ZDOID.None,
            false,
            reason);
    }

    private static bool IsPlainSpeech(string text)
    {
        return !string.IsNullOrWhiteSpace(text) && text.Length <= MaximumSpeechLength &&
            text.IndexOf('<') < 0 && text.IndexOf('>') < 0 &&
            !text.Any(char.IsControl);
    }

}
