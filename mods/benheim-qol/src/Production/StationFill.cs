using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Production;

internal static class StationFill
{
    private const float StateUpdateTimeoutSeconds = 1f;

    private static readonly MethodInfo SmelterAddOre =
        AccessTools.Method(typeof(Smelter), "OnAddOre");
    private static readonly MethodInfo SmelterAddFuel =
        AccessTools.Method(typeof(Smelter), "OnAddFuel");
    private static readonly MethodInfo SmelterGetQueueSize =
        AccessTools.Method(typeof(Smelter), "GetQueueSize");
    private static readonly MethodInfo SmelterGetFuel =
        AccessTools.Method(typeof(Smelter), "GetFuel");
    private static readonly FieldInfo SmelterNetView =
        AccessTools.Field(typeof(Smelter), "m_nview");
    private static readonly MethodInfo ShieldGeneratorAddFuel =
        AccessTools.Method(typeof(ShieldGenerator), "OnAddFuel");
    private static readonly MethodInfo ShieldGeneratorGetFuel =
        AccessTools.Method(typeof(ShieldGenerator), "GetFuel");
    private static readonly FieldInfo ShieldGeneratorNetView =
        AccessTools.Field(typeof(ShieldGenerator), "m_nview");
    private static readonly MethodInfo CookingAddFood =
        AccessTools.Method(typeof(CookingStation), "OnAddFoodSwitch");
    private static readonly MethodInfo CookingHaveDoneItem =
        AccessTools.Method(typeof(CookingStation), "HaveDoneItem");
    private static readonly MethodInfo CookingAddFuel =
        AccessTools.Method(typeof(CookingStation), "OnAddFuelSwitch");
    private static readonly MethodInfo CookingGetFuel =
        AccessTools.Method(typeof(CookingStation), "GetFuel");
    private static readonly FieldInfo CookingNetView =
        AccessTools.Field(typeof(CookingStation), "m_nview");

    private static readonly HashSet<string> ActiveInputs = new HashSet<string>();
    private static bool invokingVanilla;

    internal static bool IsInvokingVanilla => invokingVanilla;

    internal static void RegisterSmelterBatchRpc(Smelter station)
    {
        RemoteSmelterBatch.Register(station);
    }

    internal static bool TryStartSmelterOre(
        Smelter station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        if (InputState.IsShiftHeld() && user == Player.m_localPlayer && item == null)
        {
            item = RemoteSmelterBatch.SelectFirstMaterial(
                station,
                user.GetInventory(),
                null,
                RemoteSmelterBatch.OreInput);
        }
        if (RemoteSmelterBatch.ShouldUse(station, user, invokingVanilla))
        {
            return RemoteSmelterBatch.TryStart(station, user, item, RemoteSmelterBatch.OreInput);
        }

        return TryStart(
            station,
            user,
            "smelter_input",
            () => Convert.ToSingle(SmelterGetQueueSize.Invoke(station, null)),
            station.m_maxOre,
            CreateAddOne(SmelterAddOre, station, switchRef, user, item),
            () => ReadSyncState(station, SmelterNetView),
            item?.m_shared?.m_name);
    }

    internal static bool TryStartSmelterFuel(
        Smelter station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        if (RemoteSmelterBatch.ShouldUse(station, user, invokingVanilla))
        {
            return RemoteSmelterBatch.TryStart(station, user, item, RemoteSmelterBatch.FuelInput);
        }

        return TryStart(
            station,
            user,
            "smelter_fuel",
            () => Convert.ToSingle(SmelterGetFuel.Invoke(station, null)),
            station.m_maxFuel,
            CreateAddOne(SmelterAddFuel, station, switchRef, user, item),
            () => ReadSyncState(station, SmelterNetView),
            item?.m_shared?.m_name);
    }

    internal static bool TryStartShieldGeneratorFuel(
        ShieldGenerator station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        return TryStart(
            station,
            user,
            "shield_generator_fuel",
            () => Convert.ToSingle(ShieldGeneratorGetFuel.Invoke(station, null)),
            station.m_maxFuel,
            CreateAddOne(ShieldGeneratorAddFuel, station, switchRef, user, item),
            () => ReadSyncState(station, ShieldGeneratorNetView),
            item?.m_shared?.m_name);
    }

    internal static bool TryStartCookingFood(
        CookingStation station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        // Leave the player's direct ready-food interaction entirely native.
        // During an active auto-fill, CreateCookingAddOne repeats the same gate
        // so food that finishes mid-batch is never taken as a side effect.
        if (item == null && InputState.IsShiftHeld() && user == Player.m_localPlayer &&
            CookingHaveDoneItem.Invoke(station, null) is true)
        {
            return false;
        }

        return TryStart(
            station,
            user,
            "cooking_input",
            () => CountOccupiedCookingSlots(station),
            station.m_slots.Length,
            CreateCookingAddOne(station, switchRef, user, item),
            () => ReadSyncState(station, CookingNetView),
            item?.m_shared?.m_name);
    }

    internal static bool TryStartCookingFuel(
        CookingStation station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        return TryStart(
            station,
            user,
            "cooking_fuel",
            () => Convert.ToSingle(CookingGetFuel.Invoke(station, null)),
            station.m_maxFuel,
            CreateAddOne(CookingAddFuel, station, switchRef, user, item),
            () => ReadSyncState(station, CookingNetView),
            item?.m_shared?.m_name);
    }

    private static bool TryStart(
        MonoBehaviour station,
        Humanoid user,
        string inputKind,
        Func<float> getLevel,
        float capacity,
        Func<bool> addOne,
        Func<StationSyncState> getSyncState,
        string? selectedItemName)
    {
        if (invokingVanilla || !InputState.IsShiftHeld())
        {
            return false;
        }

        float level = getLevel();
        if (user != Player.m_localPlayer || level >= capacity)
        {
            return false;
        }

        string activeKey = $"{station.GetInstanceID()}:{inputKind}";
        if (!ActiveInputs.Add(activeKey))
        {
            return true;
        }

        try
        {
            StationSyncState sync = getSyncState();
            string stationIdentity = GetStationIdentity(station);

            Diagnostics.Event(
                "Production",
                "station_fill_started",
                $"station={stationIdentity} input={inputKind} " +
                $"level={level:0.###} capacity={capacity:0.###} " +
                $"selected={(selectedItemName == null ? "auto" : Diagnostics.Flatten(selectedItemName))} " +
                sync.Describe());
            station.StartCoroutine(Fill(
                station,
                user,
                activeKey,
                inputKind,
                stationIdentity,
                getLevel,
                capacity,
                addOne,
                getSyncState));
        }
        catch
        {
            ActiveInputs.Remove(activeKey);
            throw;
        }
        return true;
    }

    private static IEnumerator Fill(
        MonoBehaviour station,
        Humanoid user,
        string activeKey,
        string inputKind,
        string stationIdentity,
        Func<float> getLevel,
        float capacity,
        Func<bool> addOne,
        Func<StationSyncState> getSyncState)
    {
        float startedAt = Time.unscaledTime;
        int attempted = 0;
        int confirmed = 0;
        string result = "complete";
        float lastLevel = 0f;
        StationSyncState lastSync = default;

        try
        {
            lastLevel = getLevel();
            lastSync = getSyncState();
            while (station && getLevel() < capacity)
            {
                float before = getLevel();
                if (!addOne())
                {
                    lastLevel = getLevel();
                    lastSync = getSyncState();
                    result = "vanilla_rejected";
                    break;
                }

                attempted++;
                float waitStartedAt = Time.unscaledTime;
                float deadline = waitStartedAt + StateUpdateTimeoutSeconds;
                while (station && getLevel() <= before && Time.unscaledTime < deadline)
                {
                    yield return null;
                }

                if (!station)
                {
                    result = "station_destroyed";
                    break;
                }

                lastLevel = getLevel();
                lastSync = getSyncState();
                if (lastLevel <= before)
                {
                    result = "state_update_timeout";
                    break;
                }

                confirmed++;
            }
        }
        finally
        {
            ActiveInputs.Remove(activeKey);
        }

        if (confirmed > 0 && user)
        {
            user.Message(
                MessageHud.MessageType.Center,
                confirmed == 1 ? "Filled 1 item" : $"Filled {confirmed} items");
        }

        Diagnostics.Event(
            "Production",
            "station_fill_finished",
            $"station={stationIdentity} input={inputKind} " +
            $"attempted={attempted} confirmed={confirmed} result={result} " +
            $"level={lastLevel:0.###}/{capacity:0.###} " +
            $"elapsed={Time.unscaledTime - startedAt:0.###} {lastSync.Describe()}");
    }

    private static bool InvokeVanilla(
        MethodInfo method,
        object station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        invokingVanilla = true;
        try
        {
            return method.Invoke(station, new object?[] { switchRef, user, item }) is true;
        }
        catch (TargetInvocationException ex)
        {
            Plugin.Log.LogWarning(
                $"Station fill callback failed: {(ex.InnerException ?? ex).Message}");
            return false;
        }
        finally
        {
            invokingVanilla = false;
        }
    }

    private static Func<bool> CreateAddOne(
        MethodInfo method,
        object station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? firstItem)
    {
        string? selectedItemName = firstItem?.m_shared?.m_name;
        return () =>
        {
            ItemDrop.ItemData? item = selectedItemName != null
                ? user.GetInventory().GetItem(selectedItemName)
                : null;
            if (selectedItemName != null && item == null)
            {
                return false;
            }
            return InvokeVanilla(method, station, switchRef, user, item);
        };
    }

    private static Func<bool> CreateCookingAddOne(
        CookingStation station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? firstItem)
    {
        string? selectedItemName = firstItem?.m_shared?.m_name;
        return () =>
        {
            ItemDrop.ItemData? item = selectedItemName != null
                ? user.GetInventory().GetItem(selectedItemName)
                : null;
            if (selectedItemName != null && item == null)
            {
                return false;
            }

            // Valheim's null-item path both chooses the first compatible food
            // and awards the native Cooking skill gain. It also takes a ready
            // output, so stop the batch before invoking that path when any
            // finished food is waiting. The player's ordinary interaction is
            // left untouched when the batch never starts.
            if (item == null && CookingHaveDoneItem.Invoke(station, null) is true)
            {
                return false;
            }

            return InvokeVanilla(CookingAddFood, station, switchRef, user, item);
        };
    }

    private static StationSyncState ReadSyncState(
        MonoBehaviour station,
        FieldInfo netViewField)
    {
        ZNetView? netView = netViewField.GetValue(station) as ZNetView;
        ZDO? zdo = netView?.GetZDO();
        return new StationSyncState(netView, zdo);
    }

    private static string GetStationIdentity(MonoBehaviour station)
    {
        string fallback = station.gameObject.name;
        try
        {
            string prefabName = Utils.GetPrefabName(station.gameObject);
            string identity = string.IsNullOrEmpty(prefabName) ? fallback : prefabName;
            return $"{Diagnostics.Flatten(identity)}#{station.GetInstanceID()}";
        }
        catch
        {
            return $"{Diagnostics.Flatten(fallback)}#{station.GetInstanceID()}";
        }
    }

    private static float CountOccupiedCookingSlots(CookingStation station)
    {
        ZNetView? netView = CookingNetView.GetValue(station) as ZNetView;
        ZDO? zdo = netView?.GetZDO();
        if (zdo == null)
        {
            return station.m_slots.Length;
        }

        int occupied = 0;
        for (int index = 0; index < station.m_slots.Length; index++)
        {
            if (!string.IsNullOrEmpty(zdo.GetString($"slot{index}")))
            {
                occupied++;
            }
        }

        return occupied;
    }

    private readonly struct StationSyncState
    {
        private readonly bool valid;
        private readonly bool owner;
        private readonly bool hasOwner;
        private readonly long ownerId;
        private readonly uint dataRevision;

        internal StationSyncState(ZNetView? netView, ZDO? zdo)
        {
            valid = netView?.IsValid() == true;
            owner = netView?.IsOwner() == true;
            hasOwner = zdo?.HasOwner() == true;
            ownerId = zdo?.GetOwner() ?? 0L;
            dataRevision = zdo?.DataRevision ?? 0u;
        }

        internal string Describe()
        {
            string ownerKind = !hasOwner ? "none" : owner ? "local" : "remote";
            return $"owner={ownerKind} owner_id={ownerId} zdo_valid={Diagnostics.Bool(valid)} " +
                $"data_revision={dataRevision}";
        }
    }
}
