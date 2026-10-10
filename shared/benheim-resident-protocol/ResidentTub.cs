using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Benheim.Resident;

/// <summary>
/// Owns the small piece of shared state attached to Valheim's existing tub.
/// George remains a client-local decorative child; this helper creates no
/// network prefab and never destroys or replaces the native tub.
/// </summary>
internal static class ResidentTub
{
    private const string NativeTubPrefabName = "piece_bathtub";
    private const string ResidentSeatPointName = "SitPoint (3)";
    private const int MaxOperationIdLength = 128;

    // ZNetView keeps RPC delegates per instance. A weak table makes repeated
    // lifecycle callbacks idempotent without retaining unloaded native tubs.
    private static readonly ConditionalWeakTable<ZNetView, Registration> RegisteredViews = new();

    private sealed class Registration
    {
    }

    /// <summary>
    /// Attach the owner RPC to one loaded native bathtub. Call this from the
    /// Smelter lifecycle on clients and the server, after the native ZNetView
    /// has initialized its ZDO.
    /// </summary>
    internal static bool Attach(Smelter smelter)
    {
        if (!smelter || !TryGet(smelter.gameObject, out ZNetView view))
        {
            return false;
        }

        InstallOwnerHandler(view);
        return true;
    }

    /// <summary>
    /// Resolve a target only when it belongs to a live native piece_bathtub.
    /// Child interaction components are allowed; the returned view is the
    /// native object's own persistent network view.
    /// </summary>
    internal static bool TryGet(GameObject target, out ZNetView view)
    {
        view = null!;
        if (!target)
        {
            return false;
        }

        ZNetView? candidate = target.GetComponent<ZNetView>();
        if (!candidate)
        {
            candidate = target.GetComponentInParent<ZNetView>();
        }

        if (!candidate || !candidate.IsValid() || candidate.GetZDO() == null
            || !string.Equals(
                Utils.GetPrefabName(candidate.gameObject),
                NativeTubPrefabName,
                StringComparison.Ordinal))
        {
            return false;
        }

        view = candidate;
        return true;
    }

    /// <summary>
    /// Return the native Chair component named SitPoint (3) when it exposes
    /// its attach point. The target is found by name rather than by an index
    /// into a mutable component list, so other seats keep their native behavior.
    /// </summary>
    internal static Chair? FindSeat(GameObject tub)
    {
        if (!TryGet(tub, out ZNetView view))
        {
            return null;
        }

        foreach (Chair candidate in view.GetComponentsInChildren<Chair>(includeInactive: true))
        {
            if (candidate
                && string.Equals(candidate.name, ResidentSeatPointName, StringComparison.Ordinal)
                && candidate.m_attachPoint)
            {
                return candidate;
            }
        }

        return null;
    }

    internal static bool IsInvited(ZDO? zdo) => zdo != null && zdo.GetBool(ResidentProtocol.InvitedKey);

    internal static int Generation(ZDO? zdo) => zdo == null ? 0 : zdo.GetInt(ResidentProtocol.GenerationKey);

    /// <summary>
    /// Install the typed per-object callback once for this native tub view.
    /// Registering on each peer is required because Valheim routes the call to
    /// whichever peer currently owns the native ZDO.
    /// </summary>
    internal static void InstallOwnerHandler(ZNetView view)
    {
        if (!IsNativeTubView(view) || RegisteredViews.TryGetValue(view, out _))
        {
            return;
        }

        view.Register<long, string, bool, int, ZDOID, double>(
            ResidentProtocol.OwnerPlacementRpc,
            (sender, requestServerUid, operationId, desired, expectedGeneration, requesterCharacter, expiresAt) =>
                HandleOwnerPlacement(
                    view,
                    sender,
                    requestServerUid,
                    operationId,
                    desired,
                    expectedGeneration,
                    requesterCharacter,
                    expiresAt));
        RegisteredViews.Add(view, new Registration());
    }

    /// <summary>
    /// Apply one generation-checked placement change on the current native
    /// owner. Both local authoritative calls and the server-authenticated RPC
    /// use this validation and write path.
    /// </summary>
    internal static bool TryApply(
        ZNetView view,
        bool desired,
        int expectedGeneration,
        ZDOID requesterCharacter,
        out string reason,
        out int generation)
    {
        generation = 0;
        if (!IsNativeTubView(view))
        {
            reason = "native_tub_required";
            return false;
        }

        if (!view.IsOwner())
        {
            reason = "not_current_owner";
            return false;
        }

        ZDO zdo = view.GetZDO();
        if (!zdo.Persistent)
        {
            reason = "native_tub_not_persistent";
            return false;
        }
        int currentGeneration = Generation(zdo);
        generation = currentGeneration;
        if (currentGeneration < 0 || currentGeneration == int.MaxValue)
        {
            reason = "generation_invalid";
            return false;
        }

        if (expectedGeneration != currentGeneration)
        {
            reason = "generation_conflict";
            return false;
        }

        if (requesterCharacter == ZDOID.None || !TryFindRequester(requesterCharacter, out Player requester))
        {
            reason = "requester_unavailable";
            return false;
        }

        if (requester.IsDead())
        {
            reason = "requester_dead";
            return false;
        }

        if (desired)
        {
            Chair? seat = FindSeat(view.gameObject);
            if (!seat || !seat.m_attachPoint)
            {
                reason = "native_seat_missing";
                return false;
            }

            // Do not place George on top of a player who is already using his
            // fixed seat. The other tub seats remain native and unaffected.
            if (seat.IsInUse())
            {
                reason = "native_seat_in_use";
                return false;
            }

        }

        // Keep access policy symmetric: an invite and its dismissal both
        // target the same protected piece of the player's base.
        if (!HasWardAccess(requester.GetPlayerID(), view.transform.position, out reason))
        {
            return false;
        }

        int nextGeneration = currentGeneration + 1;
        zdo.Set(ResidentProtocol.InvitedKey, desired);
        zdo.Set(ResidentProtocol.GenerationKey, nextGeneration);
        generation = nextGeneration;
        reason = "accepted";
        return true;
    }

    private static bool IsNativeTubView(ZNetView? view)
    {
        return view
            && view.IsValid()
            && view.GetZDO() != null
            && string.Equals(
                Utils.GetPrefabName(view.gameObject),
                NativeTubPrefabName,
                StringComparison.Ordinal);
    }

    private static bool TryFindRequester(ZDOID characterId, out Player player)
    {
        player = null!;
        if (ZNetScene.instance == null)
        {
            return false;
        }

        GameObject? requesterObject = ZNetScene.instance.FindInstance(characterId);
        if (!requesterObject)
        {
            return false;
        }

        Player? candidate = requesterObject.GetComponent<Player>();
        ZNetView? requesterView = candidate ? candidate.GetComponent<ZNetView>() : null;
        if (!candidate || !requesterView || !requesterView.IsValid())
        {
            return false;
        }

        if (requesterView.GetZDO().m_uid != characterId)
        {
            return false;
        }

        player = candidate;
        return true;
    }

    /// <summary>
    /// Check each active overlapping ward using the requester's identity. The
    /// built-in PrivateArea.CheckAccess uses Player.m_localPlayer, which is
    /// wrong when this routed owner is a different peer or dedicated server.
    /// </summary>
    private static bool HasWardAccess(long requesterPlayerId, Vector3 tubPosition, out string reason)
    {
        foreach (PrivateArea ward in UnityEngine.Object.FindObjectsByType<PrivateArea>(FindObjectsSortMode.None))
        {
            if (!ward)
            {
                continue;
            }

            ZNetView? wardView = ward.GetComponent<ZNetView>();
            if (!wardView || !wardView.IsValid())
            {
                // A PrivateArea without a valid ZDO is not an active native
                // ward, matching the guardstone's own IsEnabled behavior.
                continue;
            }

            ZDO wardZdo = wardView.GetZDO();
            if (!wardZdo.GetBool(ZDOVars.s_enabled))
            {
                continue;
            }

            Vector2 offset = new Vector2(
                tubPosition.x - ward.transform.position.x,
                tubPosition.z - ward.transform.position.z);
            if (offset.sqrMagnitude >= ward.m_radius * ward.m_radius)
            {
                continue;
            }

            Piece? piece = ward.GetComponent<Piece>();
            if (piece && piece.GetCreator() == requesterPlayerId)
            {
                continue;
            }

            bool permitted = false;
            int permittedCount = wardZdo.GetInt(ZDOVars.s_permitted);
            for (int index = 0; index < permittedCount; index++)
            {
                if (wardZdo.GetLong("pu_id" + index) == requesterPlayerId)
                {
                    permitted = true;
                    break;
                }
            }

            if (!permitted)
            {
                reason = "ward_access_denied";
                return false;
            }
        }

        reason = "access_granted";
        return true;
    }

    private static void HandleOwnerPlacement(
        ZNetView view,
        long sender,
        long requestServerUid,
        string operationId,
        bool desired,
        int expectedGeneration,
        ZDOID requesterCharacter,
        double expiresAt)
    {
        long serverUid = ExpectedServerUid();
        bool success = false;
        string reason = "sender_not_server";
        int generation = 0;
        ZDOID tubId = ZDOID.None;
        if (IsNativeTubView(view) && IsNativeTubViewForThisHandler(requestServerUid, serverUid, sender))
        {
            tubId = view.GetZDO().m_uid;
            if (double.IsNaN(expiresAt) || double.IsInfinity(expiresAt))
            {
                reason = "deadline_invalid";
                generation = Generation(view.GetZDO());
            }
            else if (ZNet.instance == null || ZNet.instance.GetTimeSeconds() >= expiresAt)
            {
                reason = "deadline_expired";
                generation = Generation(view.GetZDO());
            }
            else if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > MaxOperationIdLength)
            {
                reason = "operation_id_invalid";
                generation = Generation(view.GetZDO());
            }
            else
            {
                success = TryApply(
                    view,
                    desired,
                    expectedGeneration,
                    requesterCharacter,
                    out reason,
                    out generation);
                if (success)
                {
                    // Ensure the server observes this ZDO before it processes
                    // the routed acknowledgement and marks the saved sector.
                    ZDOMan.instance?.ForceSendZDO(serverUid, tubId);
                }
            }
        }

        if (serverUid != 0L && ZRoutedRpc.instance != null)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(
                serverUid,
                ResidentProtocol.OwnerPlacementResultRpc,
                operationId ?? string.Empty,
                tubId,
                success,
                reason,
                generation);
        }
    }

    private static long ExpectedServerUid()
    {
        if (ZNet.instance == null)
        {
            return 0L;
        }

        return ZNet.instance.IsServer()
            ? ZNet.GetUID()
            : ZNet.instance.GetServerPeer()?.m_uid ?? 0L;
    }

    private static bool IsNativeTubViewForThisHandler(long requestServerUid, long serverUid, long sender)
    {
        return serverUid != 0L && requestServerUid == serverUid && sender == serverUid;
    }
}
