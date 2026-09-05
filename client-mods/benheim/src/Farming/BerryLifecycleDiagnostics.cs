using System;
using System.Globalization;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Farming;

// PlantEverything at f50a18f0aea3fda933ae70c2029428adceae0a6b (GPL-3.0),
// Advize_PlantEverything/Patches/ShowPickableSpawnerPatches.cs and
// HoverTextPatches.cs, provides evidence for the Awake/SetPicked observation
// seams and native picked timestamp. No upstream implementation is copied.
internal static class BerryLifecycleDiagnostics
{
    private static bool failureReported;

    internal sealed class State
    {
        internal string BushId = string.Empty;
        internal string NetworkId = string.Empty;
        internal string Prefab = string.Empty;
        internal string? CycleId;
        internal Vector3 Position;
        internal long WorldId;
        internal long CycleStart;
        internal long ObservedTime;
        internal float SelectedSeconds;
        internal float ConfiguredSeconds;
        internal bool Planted;
        internal bool Owner;
        internal bool Picked;
        internal bool Harvestable;
    }

    internal static State? Capture(Pickable pickable)
    {
        try
        {
            if (!PlantableBerries.IsBerryBush(pickable.gameObject)) return null;
            ZNetView? view = pickable.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !ZNet.instance) return null;

            ZDO zdo = view.GetZDO();
            Vector3 position = zdo.GetPosition();
            long worldId = ZNet.instance.GetWorldUID();
            string prefab = Utils.GetPrefabName(pickable.gameObject);
            // ZDO IDs are remapped by native world loading. The prefab and exact
            // persisted position let a cycle be correlated within the same world
            // across reloads; retain the current network ID to disambiguate live
            // instances. No diagnostic identity is written into the world.
            string bushId = string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2:x8}:{3:x8}:{4:x8}",
                worldId, prefab, BitConverter.SingleToInt32Bits(position.x),
                BitConverter.SingleToInt32Bits(position.y), BitConverter.SingleToInt32Bits(position.z));
            long started = zdo.GetLong(ZDOVars.s_pickedTime, 0L);
            return new State
            {
                BushId = bushId,
                NetworkId = zdo.m_uid.ToString(),
                Prefab = prefab,
                CycleId = started > 1L ? bushId + ":" + started.ToString(CultureInfo.InvariantCulture) : null,
                Position = position,
                WorldId = worldId,
                CycleStart = started,
                ObservedTime = ZNet.instance.GetTime().Ticks,
                SelectedSeconds = started > 1L ? PlantableBerries.ResolveBerryRespawnSeconds(position, started) : 0f,
                ConfiguredSeconds = pickable.m_respawnTimeMinutes * 60f,
                Planted = zdo.GetLong(ZDOVars.s_creator, 0L) != 0L,
                Owner = view.IsOwner(),
                Picked = pickable.GetPicked(),
                Harvestable = pickable.CanBePicked(),
            };
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
            return null;
        }
    }

    internal static void Loaded(Pickable pickable)
    {
        State? state = Capture(pickable);
        if (state == null) return;
        Emit(() => Event("berry_state_loaded", state, false)
            // Awake can precede Piece.SetCreator for a new placement. An absent
            // marker here does not yet establish that the bush is natural.
            .String("origin", state.Planted ? "planted" : "unmarked_at_load")
            .String("observation_source", "awake")
            .Boolean("cycle_start_observed", false));
    }

    internal static void PickedChanged(Pickable pickable, State? before)
    {
        State? after = Capture(pickable);
        // Only the owner writes the timestamp in SetPicked. A receiving peer can
        // observe the RPC before its ZDO timestamp arrives, so it cannot safely
        // identify the new cycle. Loaded state remains observable on every peer.
        if (before == null || after == null || !before.Owner || !after.Owner) return;
        if (after.Picked && after.CycleStart > 1L
            && (!before.Picked || before.CycleStart != after.CycleStart))
        {
            Emit(() => Event("berry_cycle_started", after, true)
                .String("origin", after.Planted ? "planted" : "natural")
                .String("observation_source", "set_picked")
                .String("previous_cycle_id", before.CycleId)
                .Boolean("cycle_start_observed", true));
        }
        else if (before.Picked && !after.Picked && after.Harvestable)
        {
            Emit(() => Event("berry_harvestable", after, true)
                .String("origin", after.Planted ? "planted" : "natural")
                .String("observation_source", "set_picked")
                .Boolean("cycle_start_observed", false)
                .Boolean("previous_picked", before.Picked));
        }
    }

    internal static void HarvestCompleted(Pickable pickable, State? before, long sender)
    {
        State? after = Capture(pickable);
        if (before == null || after == null) return;
        bool harvested = before.Owner && !before.Picked && after.Owner && after.Picked;
        // The native owner RPC has already authored its drops and synchronously
        // applied SetPicked locally before returning. This proves a harvest,
        // rather than treating an Interact return value as success.
        Emit(() => Event(harvested ? "berry_harvested" : "berry_harvest_rejected", after, harvested)
            .String("origin", after.Planted ? "planted" : "natural")
            .String("observation_source", "rpc_pick")
            .String("previous_cycle_id", before.CycleId)
            .Integer("requester_peer", sender)
            .String("reason", harvested ? "native_harvest_completed"
                : !before.Owner ? "not_owner" : before.Picked ? "already_picked" : "picked_state_unchanged"));
    }

    private static DiagnosticEvent Event(string name, State state, bool transitionObserved)
    {
        DiagnosticEvent result = DiagnosticEvent.Create("Farming", name)
            .String("bush_id", state.BushId)
            .String("network_object_id", state.NetworkId)
            .String("prefab", state.Prefab)
            .Integer("world_uid", state.WorldId)
            .Number("position_x", state.Position.x)
            .Number("position_y", state.Position.y)
            .Number("position_z", state.Position.z)
            .String("cycle_id", state.CycleId)
            .Boolean("cycle_known", state.CycleId != null)
            .Boolean("planted", state.Planted)
            .Boolean("owner", state.Owner)
            .Boolean("picked", state.Picked)
            .Boolean("harvestable", state.Harvestable)
            .Boolean("transition_observed", transitionObserved)
            .Integer("observed_game_ticks", state.ObservedTime)
            .Number("configured_respawn_seconds", state.ConfiguredSeconds);
        if (state.CycleId != null)
        {
            result.Integer("cycle_start_game_ticks", state.CycleStart)
                .Number("selected_duration_seconds", state.SelectedSeconds)
                .Number("elapsed_game_seconds", (state.ObservedTime - state.CycleStart) / (double)TimeSpan.TicksPerSecond);
        }
        return result;
    }

    internal static void Emit(Func<DiagnosticEvent> create)
    {
        try { Diagnostics.Emit(create()); }
        catch (Exception exception) { ReportFailure(exception); }
    }

    private static void ReportFailure(Exception exception)
    {
        if (failureReported) return;
        failureReported = true;
        // This can run from a Harmony prefix. Even the fallback logger must
        // remain observational when BepInEx's logging destination fails.
        try { Plugin.Log.LogWarning($"Berry diagnostics failed: {exception.GetType().Name}"); }
        catch { }
    }
}

[HarmonyPatch(typeof(Pickable), "Awake")]
internal static class BerryLoadedDiagnosticPatch
{
    [HarmonyPostfix]
    internal static void Postfix(Pickable __instance) => BerryLifecycleDiagnostics.Loaded(__instance);
}

[HarmonyPatch(typeof(Pickable), nameof(Pickable.SetPicked))]
internal static class BerryPickedDiagnosticPatch
{
    [HarmonyPrefix]
    internal static void Prefix(Pickable __instance, out BerryLifecycleDiagnostics.State? __state) =>
        __state = BerryLifecycleDiagnostics.Capture(__instance);

    [HarmonyPostfix]
    internal static void Postfix(Pickable __instance, BerryLifecycleDiagnostics.State? __state) =>
        BerryLifecycleDiagnostics.PickedChanged(__instance, __state);
}

[HarmonyPatch(typeof(Pickable), "RPC_Pick")]
internal static class BerryHarvestDiagnosticPatch
{
    [HarmonyPrefix]
    internal static void Prefix(Pickable __instance, out BerryLifecycleDiagnostics.State? __state) =>
        __state = BerryLifecycleDiagnostics.Capture(__instance);

    [HarmonyPostfix]
    internal static void Postfix(Pickable __instance, long sender, BerryLifecycleDiagnostics.State? __state) =>
        BerryLifecycleDiagnostics.HarvestCompleted(__instance, __state, sender);
}
