using System;
using System.Linq;
using Benheim.Resident;
using UnityEngine;

namespace BenheimServerSupport;

// Strict package decoding, result encoding, and shared native-state validators.
internal static partial class ResidentServer
{

    private static bool TryReadPlacementRequest(
        ZPackage package,
        out string operationId,
        out ZDOID tubId,
        out bool desired,
        out int expectedGeneration)
    {
        operationId = string.Empty;
        tubId = ZDOID.None;
        desired = false;
        expectedGeneration = -1;
        try
        {
            if (package.Size() > MaximumRpcPackageBytes)
            {
                return false;
            }

            operationId = package.ReadString();
            tubId = package.ReadZDOID();
            desired = package.ReadBool();
            expectedGeneration = package.ReadInt();
            return IsOperationId(operationId) && !tubId.IsNone() &&
                expectedGeneration >= 0 && package.GetPos() == package.Size();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadEncounterRequest(
        ZPackage package,
        out string operationId,
        out ZDOID tubId,
        out int generation)
    {
        operationId = string.Empty;
        tubId = ZDOID.None;
        generation = -1;
        try
        {
            if (package.Size() > MaximumRpcPackageBytes)
            {
                return false;
            }

            operationId = package.ReadString();
            tubId = package.ReadZDOID();
            generation = package.ReadInt();
            return IsOperationId(operationId) && !tubId.IsNone() &&
                generation >= 0 && package.GetPos() == package.Size();
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadSpeechReply(
        ZPackage package,
        out string operationId,
        out ZDOID tubId,
        out int generation,
        out bool speak,
        out string text,
        out string reason)
    {
        operationId = string.Empty;
        tubId = ZDOID.None;
        generation = -1;
        speak = false;
        text = string.Empty;
        reason = string.Empty;
        try
        {
            if (package.Size() > MaximumRpcPackageBytes)
            {
                return false;
            }

            operationId = package.ReadString();
            tubId = package.ReadZDOID();
            generation = package.ReadInt();
            speak = package.ReadBool();
            text = package.ReadString();
            reason = package.ReadString();
            return IsOperationId(operationId) && !tubId.IsNone() && generation >= 0 &&
                text != null && reason != null && package.GetPos() == package.Size();
        }
        catch
        {
            return false;
        }
    }

    private static void RejectOrConfirmNoOp(
        ZNetPeer peer,
        string operationId,
        ZDOID tubId,
        bool desired,
        int generation)
    {
        string reason = desired ? "already_invited" : "already_dismissed";
        SendPlacementResult(peer, operationId, tubId, true, reason, generation);
        Emit("placement_confirmed", operationId, tubId, reason, "placement");
    }

    private static void RejectPlacement(
        ZNetPeer peer,
        string operationId,
        ZDOID tubId,
        bool success,
        string reason,
        int generation)
    {
        SendPlacementResult(peer, operationId, tubId, success, reason, generation);
    }

    private static void SendPlacementResult(
        ZNetPeer peer,
        string operationId,
        ZDOID tubId,
        bool success,
        string reason,
        int generation)
    {
        if (peer?.m_rpc == null)
        {
            return;
        }

        ZPackage result = new();
        result.Write(IsOperationId(operationId) ? operationId : "invalid");
        result.Write(tubId);
        result.Write(success);
        result.Write(NormalizeReason(reason, success ? "applied" : "rejected"));
        result.Write(generation);
        try
        {
            if (peer.m_rpc.IsConnected())
            {
                peer.m_rpc.Invoke(ResidentProtocol.PlacementResultRpc, result);
            }
        }
        catch (Exception exception)
        {
            Emit("placement_result_delivery_failed", operationId, tubId,
                exception.GetType().Name, "placement_result");
        }
    }

    private static void SendEncounterGrant(
        ZNetPeer peer,
        string operationId,
        ZDOID tubId,
        int generation,
        ZDOID characterId,
        bool accepted,
        string reason)
    {
        if (peer?.m_rpc == null)
        {
            return;
        }

        ZPackage grant = new();
        grant.Write(IsOperationId(operationId) ? operationId : "invalid");
        grant.Write(tubId);
        grant.Write(generation);
        grant.Write(characterId);
        grant.Write(accepted);
        grant.Write(NormalizeReason(reason, accepted ? "granted" : "rejected"));
        try
        {
            if (peer.m_rpc.IsConnected())
            {
                peer.m_rpc.Invoke(ResidentProtocol.EncounterGrantRpc, grant);
            }
        }
        catch (Exception exception)
        {
            Emit("speech_grant_delivery_failed", operationId, tubId,
                exception.GetType().Name, "encounter_grant");
        }
    }

    private static bool TryResolveRequester(ZNetPeer peer, out ZDO character)
    {
        character = null!;
        if (peer == null || !peer.IsReady() || peer.m_socket == null ||
            peer.m_characterID.IsNone() || ZDOMan.instance == null)
        {
            return false;
        }

        ZDO? candidate = ZDOMan.instance.GetZDO(peer.m_characterID);
        if (candidate == null || candidate.GetOwner() != peer.m_uid)
        {
            return false;
        }

        character = candidate;
        return true;
    }

    private static bool TryResolveTub(ZDOID id, out ZDO tub)
    {
        tub = null!;
        if (id.IsNone() || ZDOMan.instance == null)
        {
            return false;
        }

        ZDO? candidate = ZDOMan.instance.GetZDO(id);
        if (candidate == null || candidate.GetPrefab() != BathtubPrefabHash)
        {
            return false;
        }

        tub = candidate;
        return true;
    }

    private static Vector3 GetTubPosition(ZDOID id) =>
        TryResolveTub(id, out ZDO tub) ? tub.GetPosition() : new Vector3(float.NaN, 0f, 0f);

    private static bool WithinRange(Vector3 from, Vector3 to)
    {
        if (!IsFinite(from) || !IsFinite(to))
        {
            return false;
        }

        return (from - to).sqrMagnitude <= InteractionRange * InteractionRange;
    }

    private static string NormalizeReason(string reason, string fallback)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > MaximumReasonLength ||
            reason.Any(character => !char.IsLetterOrDigit(character) && character != '_'))
        {
            return fallback;
        }

        return reason;
    }

    private static bool IsOperationId(string operationId) =>
        operationId != null && operationId.Length == 32 &&
        Guid.TryParseExact(operationId, "N", out _);

    private static bool IsFinite(Vector3 value) =>
        !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
        !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
        !float.IsNaN(value.z) && !float.IsInfinity(value.z);

}
