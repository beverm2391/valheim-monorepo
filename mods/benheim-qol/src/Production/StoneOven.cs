using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.Production;

internal static class StoneOven
{
    private const string PrefabName = "piece_oven";
    private const float BakeTimeMultiplier = 0.5f;

    // Awake runs once for each locally loaded station. The conversion entries are
    // instance data, so the native owner always evaluates its own halved times.
    private static readonly HashSet<int> ObservedNativeOwners = new HashSet<int>();

    internal static void ApplyBakeTime(CookingStation station)
    {
        if (!IsStoneOven(station))
        {
            return;
        }

        ZNetView? netView = station.GetComponent<ZNetView>();
        if (netView == null || netView.GetZDO() == null)
        {
            // Match CookingStation.Awake's native runtime-instance gate. Never
            // mutate a prefab/template that a later station could clone again.
            return;
        }

        int adjusted = 0;
        foreach (CookingStation.ItemConversion conversion in station.m_conversion)
        {
            if (conversion == null)
            {
                continue;
            }

            conversion.m_cookTime *= BakeTimeMultiplier;
            adjusted++;
        }

        Diagnostics.Event(
            "StoneOven",
            "bake_time_halved",
            $"prefab={PrefabName} conversions={adjusted} multiplier={BakeTimeMultiplier:0.###}");
    }

    internal static void ObserveNativeOwner(CookingStation station)
    {
        if (!IsStoneOven(station))
        {
            return;
        }

        ZNetView? netView = station.GetComponent<ZNetView>();
        if (netView == null || !netView.IsValid() || !netView.IsOwner())
        {
            return;
        }

        if (!ObservedNativeOwners.Add(station.GetInstanceID()))
        {
            return;
        }

        Diagnostics.Event(
            "StoneOven",
            "native_owner_observed",
            $"prefab={PrefabName} conversions={station.m_conversion.Count} multiplier={BakeTimeMultiplier:0.###}");
    }

    private static bool IsStoneOven(CookingStation station)
    {
        return Utils.GetPrefabName(station.gameObject) == PrefabName;
    }
}
