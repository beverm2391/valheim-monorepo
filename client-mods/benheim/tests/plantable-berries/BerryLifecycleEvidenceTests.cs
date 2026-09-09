using System;
using System.Text.Json;
using BenheimQoL;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using UnityEngine;
using static BerryTestSupport;

internal static class BerryLifecycleEvidenceTests
{
    internal static void Run(
        ZNetScene scene, PieceTable pieceTable, GameObject[] registeredBushes,
        GameObject remoteBush, Pickable unrelated)
    {
        Require(LastEvent("plantable_berries_registered").GetProperty("count").GetInt32() == 3,
            "berry registration emits a typed prefab count");

        // Exercise production observation seams around immediate native state changes.
        // These assertions prove evidence semantics, not an elapsed in-game growth run.
        foreach (string prefab in new[] { "RaspberryBush", "BlueberryBush", "CloudberryBush" })
        {
            foreach (bool planted in new[] { false, true })
            {
                Diagnostics.TypedJson.Clear();
                GameObject observedBush = CreateBush(prefab, CreateBerryItem("EvidenceBerry"));
                Pickable observed = observedBush.GetComponent<Pickable>()!;
                ZDO observedZdo = observedBush.GetComponent<ZNetView>()!.GetZDO();
                if (planted) observedBush.AddComponent<Piece>().SetCreator(42L);

                // Loading native state never manufactures a cycle or growth transition.
                BerryLoadedDiagnosticPatch.Postfix(observed);
                JsonElement loaded = LastEvent("berry_state_loaded");
                Require(!loaded.GetProperty("transition_observed").GetBoolean()
                    && !loaded.GetProperty("cycle_start_observed").GetBoolean()
                    && !loaded.GetProperty("cycle_known").GetBoolean(),
                    "an initially ripe bush has loaded state and no invented cycle");

                UnityEngine.Random.State randomBefore = UnityEngine.Random.state;
                long nativeStart = ZNet.instance.GetTime().Ticks;
                observed.SetPicked(true);
                JsonElement started = LastEvent("berry_cycle_started");
                string cycleId = started.GetProperty("cycle_id").GetString()!;
                string bushId = started.GetProperty("bush_id").GetString()!;
                Require(started.GetProperty("planted").GetBoolean() == planted
                    && started.GetProperty("origin").GetString() == (planted ? "planted" : "natural"),
                    "cycle evidence distinguishes the native creator marker");
                Require(started.GetProperty("cycle_start_game_ticks").GetInt64() == nativeStart
                    && started.GetProperty("cycle_start_observed").GetBoolean(),
                    "cycle start is the timestamp authored by native SetPicked");
                RequireNear(started.GetProperty("selected_duration_seconds").GetSingle(),
                    PlantableBerries.ResolveBerryRespawnSeconds(observedZdo.GetPosition(), nativeStart),
                    "cycle evidence reports the actual deterministic selection");
                RequireNear(observed.m_respawnTimeMinutes, 300f,
                    "observation does not apply or alter the native timer");
                Require(UnityEngine.Random.state.Value == randomBefore.Value,
                    "diagnostic duration resolution leaves Unity random state unchanged");

                // A second native instance sees the same persisted cycle even if the
                // world loader assigned a new ZDO ID. Its Awake is still only a load.
                GameObject loadedBush = CreateBush(prefab, CreateBerryItem("ReloadBerry"));
                Pickable reloaded = loadedBush.GetComponent<Pickable>()!;
                ZDO reloadZdo = loadedBush.GetComponent<ZNetView>()!.GetZDO();
                reloadZdo.Position = observedZdo.GetPosition();
                reloadZdo.Set(ZDOVars.s_pickedTime, nativeStart);
                reloadZdo.Set(ZDOVars.s_creator, planted ? 42L : 0L);
                reloaded.LoadPickedState(true);
                int beforeLoad = Diagnostics.TypedJson.Count;
                BerryLoadedDiagnosticPatch.Postfix(reloaded);
                loaded = LastEvent("berry_state_loaded");
                Require(Diagnostics.TypedJson.Count == beforeLoad + 1
                    && loaded.GetProperty("cycle_id").GetString() == cycleId
                    && loaded.GetProperty("bush_id").GetString() == bushId
                    && loaded.GetProperty("picked").GetBoolean()
                    && !loaded.GetProperty("transition_observed").GetBoolean(),
                    "mid-cycle reload records existing state with stable cycle identity");
                ZNet.instance.WorldId++;
                BerryLoadedDiagnosticPatch.Postfix(reloaded);
                Require(LastEvent("berry_state_loaded").GetProperty("bush_id").GetString() != bushId,
                    "equal positions in different worlds cannot share a bush identity");
                ZNet.instance.WorldId--;

                observed.SetPicked(false);
                JsonElement harvestable = LastEvent("berry_harvestable");
                Require(harvestable.GetProperty("transition_observed").GetBoolean()
                    && harvestable.GetProperty("harvestable").GetBoolean()
                    && !harvestable.GetProperty("picked").GetBoolean()
                    && harvestable.GetProperty("cycle_id").GetString() == cycleId,
                    "an actual picked-to-harvestable state change identifies its cycle");
                int afterTransition = Diagnostics.TypedJson.Count;
                observed.SetPicked(false);
                Require(Diagnostics.TypedJson.Count == afterTransition,
                    "an unchanged ripe state emits no duplicate growth event");
                BerryLoadedDiagnosticPatch.Postfix(observed);
                Require(!LastEvent("berry_state_loaded").GetProperty("transition_observed").GetBoolean(),
                    "loading an already-ripe bush never claims observed growth");

                ZNet.instance.Time = ZNet.instance.Time.AddTicks(1);
                observed.Harvest(77L);
                JsonElement harvested = LastEvent("berry_harvested");
                Require(observed.NativeHarvests == 1 && observed.Picked
                    && harvested.GetProperty("previous_cycle_id").GetString() == cycleId
                    && harvested.GetProperty("cycle_id").GetString() != cycleId
                    && harvested.GetProperty("requester_peer").GetInt64() == 77L,
                    "a completed owner harvest correlates the old and newly started cycles");
                observed.Harvest(77L);
                Require(observed.NativeHarvests == 1
                    && LastEvent("berry_harvest_rejected").GetProperty("reason").GetString() == "already_picked",
                    "an empty-bush request records rejection without a false harvest");
            }
        }

        Diagnostics.TypedJson.Clear();
        Pickable remoteObserved = remoteBush.GetComponent<Pickable>()!;
        remoteObserved.SetPicked(true);
        remoteObserved.SetPicked(false);
        Require(Diagnostics.TypedJson.Count == 0,
            "non-owner state RPCs cannot claim a cycle from a potentially stale replicated timestamp");
        remoteObserved.Harvest(88L);
        Require(LastEvent("berry_harvest_rejected").GetProperty("reason").GetString() == "not_owner",
            "a non-owner harvest RPC records its native rejection");

        Diagnostics.TypedJson.Clear();
        BerryLoadedDiagnosticPatch.Postfix(unrelated);
        unrelated.SetPicked(false);
        unrelated.Harvest(99L);
        Require(Diagnostics.TypedJson.Count == 0, "unrelated Pickables produce no berry evidence");

        Pickable disabledBerry = CreateBush("BlueberryBush", CreateBerryItem("DisabledBerry")).GetComponent<Pickable>()!;
        disabledBerry.SetPicked(true);
        disabledBerry.Enabled = false;
        Diagnostics.TypedJson.Clear();
        disabledBerry.SetPicked(false);
        Require(Diagnostics.TypedJson.Count == 0, "an unavailable berry does not report a harvestable transition");

        // Capture fails in the prefix, before native SetPicked. Its fallback logger
        // also throws; neither failure may prevent that native call from running.
        ZNet.instance.FailWorldLookup = true;
        Plugin.Log.FailWarning = true;
        disabledBerry.SetPicked(true);
        Require(disabledBerry.Picked && Plugin.Log.Warnings.Count == 1,
            "capture and fallback-logger failures leave the native transition intact");
        ZNet.instance.FailWorldLookup = false;
        Plugin.Log.FailWarning = false;

        Diagnostics.FailEmission = true;
        disabledBerry.SetPicked(false);
        Require(!disabledBerry.Picked, "diagnostic sink failure cannot interrupt native SetPicked");
        disabledBerry.Harvest(123L);
        Require(disabledBerry.Picked && disabledBerry.NativeHarvests == 1 && Plugin.Log.Warnings.Count == 1,
            "diagnostic delivery failures preserve native harvest and keep fallback attempts bounded");

        // Prove that successful registration stays successful when its event cannot
        // be delivered. The earlier failed fallback attempt remains rate-limited.
        foreach (GameObject bush in registeredBushes) pieceTable.m_pieces.Remove(bush);
        int errorsBefore = Plugin.Log.Errors.Count;
        Plugin.Log.FailWarning = true;
        PlantableBerries.TryRegister(scene);
        foreach (GameObject bush in registeredBushes)
        {
            Require(pieceTable.m_pieces.Contains(bush), "diagnostic failure must not change successful registration");
        }
        Require(Plugin.Log.Errors.Count == errorsBefore,
            "diagnostic failure must not report registered berries unavailable");
        Plugin.Log.FailError = true;
        PlantableBerries.TryRegister(new ZNetScene());
        Require(Plugin.Log.Errors.Count == errorsBefore + 1,
            "actual registration failure remains contained even when both logging destinations throw");
        Plugin.Log.FailError = false;
        Plugin.Log.FailWarning = false;
        Diagnostics.FailEmission = false;
    }

    static JsonElement LastEvent(string name)
    {
        for (int index = Diagnostics.TypedJson.Count - 1; index >= 0; index--)
        {
            using JsonDocument document = JsonDocument.Parse(Diagnostics.TypedJson[index]);
            if (document.RootElement.GetProperty("event").GetString() == name)
                return document.RootElement.Clone();
        }
        throw new InvalidOperationException($"No typed event named {name}");
    }
}
