using System;
using System.Collections.Generic;
using System.Linq;
using BenheimQoL.Infrastructure;
using Benheim.Resident;
using SoftReferenceableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BenheimQoL.GreydwarfResident;

/// <summary>
/// Creates the accepted decorative donor at a native seat. Shared placement
/// and persistence belong to the tub's native ZDO and Server Support protocol.
/// </summary>
public static class GreydwarfResidentRuntime
{
    private static readonly Dictionary<Chair, GreydwarfResidentBehaviour> residents = new();
    private static bool patchesAvailable;

    internal static bool IsWorldReady => Player.m_localPlayer && ZoneSystem.instance &&
        ZNetScene.instance && ZNet.instance;

    internal static bool IsEnabled => patchesAvailable && HealthReporting.GameplayActionsEnabled;

    internal static void Initialize(bool available)
    {
        Reset();
        patchesAvailable = available;
    }

    public static GameObject CreateAtSeat(Chair seat)
    {
        ResidentDiagnostics.Emit("create_attempt", "explicit_seat");
        if (!IsEnabled) return Reject("feature_unavailable");
        if (!IsWorldReady)
            return Reject("world_not_ready");
        if (!ResidentClient.Available) return Reject("compatible_peers_required");
        Piece? tub = seat ? seat.GetComponentInParent<Piece>() : null;
        if (!seat || !seat.isActiveAndEnabled || !seat.m_attachPoint || !tub ||
            Utils.GetPrefabName(tub.gameObject) != "piece_bathtub")
            return Reject("native_tub_seat_required");
        // Match native occupancy without invoking our reservation postfix:
        // the saved invitation reserves this seat before the visual exists.
        if (Player.GetClosestPlayer(seat.m_attachPoint.position, .05f) != null) return Reject("seat_in_use");
        // A disabled resident still owns its asset lease and seat association.
        if (residents.TryGetValue(seat, out GreydwarfResidentBehaviour? existing) && existing)
            return Reject("resident_already_assigned");

        SoftReference<GameObject> reference = default;
        bool held = false;
        GameObject? clone = null;
        try
        {
            ZoneSystem.ZoneLocation location = ZoneSystem.instance.m_locations
                .First(l => l.m_prefabName == "BogWitch_Camp");
            reference = location.m_prefab;
            reference.Load();
            held = true;
            Transform donor = reference.Asset.GetComponentsInChildren<Transform>(true)
                .First(t => t.name == "Menu_greydwarf_george");
            // Fail close to an asset change rather than cloning a live creature
            // or network object under the assumption that George is still decor.
            if (donor.GetComponentInChildren<Character>(true) ||
                donor.GetComponentInChildren<ZNetView>(true) ||
                donor.GetComponentInChildren<Rigidbody>(true) ||
                donor.GetComponentInChildren<Collider>(true))
                throw new InvalidOperationException("The native George donor is no longer decorative.");

            clone = Object.Instantiate(donor.gameObject, tub.transform, false);
            clone.name = "Benheim_GreydwarfResident";
            clone.SetActive(false);
            // George's seated hips sit above his root. Keep the proven native
            // pose and compensate relative to the supplied human seat anchor.
            clone.transform.position = seat.m_attachPoint.position +
                Vector3.up * -.19f + seat.m_attachPoint.forward * .35f;
            clone.transform.rotation = seat.m_attachPoint.rotation;
            clone.transform.localScale = Vector3.one;
            GreydwarfResidentBehaviour behaviour = clone.AddComponent<GreydwarfResidentBehaviour>();
            behaviour.HoldAssets(reference);
            held = false; // The component now owns release, including rollback.
            // Activate before fallible configuration so Unity guarantees this
            // component's OnDestroy even if validation fails. OnEnable is inert
            // until configured; the lease survives deferred rollback destruction.
            clone.SetActive(true);
            behaviour.Configure(seat, tub.transform);
            residents[seat] = behaviour;
            ResidentDiagnostics.Emit("created", "native_george", clone.GetInstanceID());
            return clone;
        }
        catch (Exception ex)
        {
            if (clone)
            {
                clone.SetActive(false);
                Object.Destroy(clone);
            }
            if (held) reference.Release();
            Plugin.Log.LogError($"Greydwarf resident creation failed: {ex}");
            ResidentDiagnostics.Emit("create_failed", ex.GetType().Name);
            throw;
        }
    }

    public static void Remove(GameObject resident)
    {
        if (!resident) return;
        GreydwarfResidentBehaviour? behaviour = resident.GetComponent<GreydwarfResidentBehaviour>();
        if (!behaviour) throw new ArgumentException("Object is not a Benheim resident.", nameof(resident));
        // Disable immediately: the native chair becomes usable and pending
        // dialogue is cleared before Unity's deferred destruction completes.
        resident.SetActive(false);
        Forget(behaviour.Seat, behaviour);
        Object.Destroy(resident);
    }

    internal static bool IsReserved(Chair seat)
    {
        return IsEnabled && ResidentClient.Available && seat &&
            ResidentTub.TryGet(seat.gameObject, out ZNetView view) &&
            ResidentTub.IsInvited(view.GetZDO()) && ResidentTub.FindSeat(view.gameObject) == seat;
    }

    internal static void Forget(Chair seat, GreydwarfResidentBehaviour resident)
    {
        if (residents.TryGetValue(seat, out var assigned) && assigned == resident)
            residents.Remove(seat);
    }

    internal static void Reset()
    {
        patchesAvailable = false;
        foreach (var resident in residents.Values.ToArray())
            if (resident) Remove(resident.gameObject);
        residents.Clear();
    }

    private static GameObject Reject(string reason)
    {
        ResidentDiagnostics.Emit("create_rejected", reason);
        throw new InvalidOperationException($"Cannot create greydwarf resident: {reason}.");
    }
}
