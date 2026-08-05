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
    private static readonly MethodInfo CookingAddFood =
        AccessTools.Method(typeof(CookingStation), "OnAddFoodSwitch");
    private static readonly MethodInfo CookingAddFuel =
        AccessTools.Method(typeof(CookingStation), "OnAddFuelSwitch");
    private static readonly MethodInfo CookingGetFuel =
        AccessTools.Method(typeof(CookingStation), "GetFuel");
    private static readonly FieldInfo CookingNetView =
        AccessTools.Field(typeof(CookingStation), "m_nview");

    private static readonly HashSet<string> ActiveInputs = new HashSet<string>();
    private static bool invokingVanilla;

    internal static bool IsInvokingVanilla => invokingVanilla;

    internal static bool TryStartSmelterOre(
        Smelter station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        return TryStart(
            station,
            user,
            "smelter_input",
            () => Convert.ToSingle(SmelterGetQueueSize.Invoke(station, null)),
            station.m_maxOre,
            CreateAddOne(SmelterAddOre, station, switchRef, user, item));
    }

    internal static bool TryStartSmelterFuel(
        Smelter station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        return TryStart(
            station,
            user,
            "smelter_fuel",
            () => Convert.ToSingle(SmelterGetFuel.Invoke(station, null)),
            station.m_maxFuel,
            CreateAddOne(SmelterAddFuel, station, switchRef, user, item));
    }

    internal static bool TryStartCookingFood(
        CookingStation station,
        Switch switchRef,
        Humanoid user,
        ItemDrop.ItemData? item)
    {
        return TryStart(
            station,
            user,
            "cooking_input",
            () => CountOccupiedCookingSlots(station),
            station.m_slots.Length,
            CreateAddOne(CookingAddFood, station, switchRef, user, item));
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
            CreateAddOne(CookingAddFuel, station, switchRef, user, item));
    }

    private static bool TryStart(
        MonoBehaviour station,
        Humanoid user,
        string inputKind,
        Func<float> getLevel,
        float capacity,
        Func<bool> addOne)
    {
        if (invokingVanilla
            || !InputState.IsShiftHeld()
            || user != Player.m_localPlayer
            || getLevel() >= capacity)
        {
            return false;
        }

        string activeKey = $"{station.GetInstanceID()}:{inputKind}";
        if (!ActiveInputs.Add(activeKey))
        {
            return true;
        }

        Diagnostics.Event(
            "Production",
            "station_fill_started",
            $"station={station.GetType().Name} input={inputKind} " +
            $"level={getLevel():0.###} capacity={capacity:0.###}");
        station.StartCoroutine(Fill(station, user, activeKey, inputKind, getLevel, capacity, addOne));
        return true;
    }

    private static IEnumerator Fill(
        MonoBehaviour station,
        Humanoid user,
        string activeKey,
        string inputKind,
        Func<float> getLevel,
        float capacity,
        Func<bool> addOne)
    {
        int added = 0;
        string result = "complete";

        try
        {
            while (station && getLevel() < capacity)
            {
                float before = getLevel();
                if (!addOne())
                {
                    result = "vanilla_rejected";
                    break;
                }

                added++;
                float deadline = Time.unscaledTime + StateUpdateTimeoutSeconds;
                while (station && getLevel() <= before && Time.unscaledTime < deadline)
                {
                    yield return null;
                }

                if (!station || getLevel() <= before)
                {
                    result = "state_update_timeout";
                    break;
                }
            }
        }
        finally
        {
            ActiveInputs.Remove(activeKey);
        }

        if (added > 0 && user)
        {
            user.Message(
                MessageHud.MessageType.Center,
                added == 1 ? "Filled 1 item" : $"Filled {added} items");
        }

        Diagnostics.Event(
            "Production",
            "station_fill_finished",
            $"station={(station ? station.GetType().Name : "destroyed")} input={inputKind} " +
            $"added={added} result={result}");
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
            ItemDrop.ItemData? item = selectedItemName == null
                ? null
                : user.GetInventory().GetItem(selectedItemName);
            return InvokeVanilla(method, station, switchRef, user, item);
        };
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
}
