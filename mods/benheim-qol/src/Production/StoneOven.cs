using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.Production;

internal static class StoneOven
{
    private const string PrefabName = "piece_oven";
    private const float BakeTimeMultiplier = 0.5f;
    private const float BurnThresholdMultiplier = 2f;

    private sealed class ConversionTiming
    {
        internal string From = "unknown";
        internal string To = "unknown";
        internal float NativeBakeSeconds;
        internal float EffectiveBakeSeconds;

        internal float NativeDoneToBurnSeconds => NativeBakeSeconds;
        internal float EffectiveDoneToBurnSeconds => EffectiveBakeSeconds;
        internal float NativeBurnThresholdSeconds =>
            NativeBakeSeconds * BurnThresholdMultiplier;
        internal float EffectiveBurnThresholdSeconds =>
            EffectiveBakeSeconds * BurnThresholdMultiplier;
    }

    private sealed class StationTiming
    {
        internal readonly List<ConversionTiming> Conversions =
            new List<ConversionTiming>();
    }

    private sealed class OwnerObservation
    {
    }

    // Awake runs once for each locally loaded station. Keep the timing snapshot
    // by component identity so a repeated callback cannot halve an entry twice.
    private static readonly ConditionalWeakTable<CookingStation, StationTiming>
        AppliedTimings = new ConditionalWeakTable<CookingStation, StationTiming>();

    private static readonly ConditionalWeakTable<CookingStation, OwnerObservation>
        ObservedNativeOwners = new ConditionalWeakTable<CookingStation, OwnerObservation>();

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

        if (AppliedTimings.TryGetValue(station, out _))
        {
            return;
        }

        StationTiming timing = new StationTiming();
        foreach (CookingStation.ItemConversion conversion in station.m_conversion)
        {
            if (conversion == null)
            {
                continue;
            }

            float nativeBakeSeconds = conversion.m_cookTime;
            float effectiveBakeSeconds = nativeBakeSeconds * BakeTimeMultiplier;
            timing.Conversions.Add(
                new ConversionTiming
                {
                    From = ItemName(conversion.m_from),
                    To = ItemName(conversion.m_to),
                    NativeBakeSeconds = nativeBakeSeconds,
                    EffectiveBakeSeconds = effectiveBakeSeconds
                });
            conversion.m_cookTime *= BakeTimeMultiplier;
        }

        AppliedTimings.Add(station, timing);

        Diagnostics.Event(
            "StoneOven",
            "bake_time_halved",
            $"prefab={PrefabName} conversions={timing.Conversions.Count} " +
            $"multiplier={FormatSeconds(BakeTimeMultiplier)}");
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

        if (ObservedNativeOwners.TryGetValue(station, out _))
        {
            return;
        }

        ObservedNativeOwners.Add(station, new OwnerObservation());

        string timingDetails = AppliedTimings.TryGetValue(station, out StationTiming? timing)
            ? FormatTimings(timing)
            : "timings=unavailable";

        Diagnostics.Event(
            "StoneOven",
            "native_owner_observed",
            $"prefab={PrefabName} conversions={station.m_conversion.Count} " +
            $"multiplier={FormatSeconds(BakeTimeMultiplier)} " +
            $"burn_rule=cook_time_x{FormatSeconds(BurnThresholdMultiplier)} {timingDetails}");
    }

    private static string FormatTimings(StationTiming timing)
    {
        if (timing.Conversions.Count == 0)
        {
            return "timings=none";
        }

        List<string> descriptions = new List<string>();
        foreach (ConversionTiming conversion in timing.Conversions)
        {
            descriptions.Add(
                $"{conversion.From}->{conversion.To}" +
                $":native_bake={FormatSeconds(conversion.NativeBakeSeconds)}" +
                $",effective_bake={FormatSeconds(conversion.EffectiveBakeSeconds)}" +
                $",native_done_to_burn={FormatSeconds(conversion.NativeDoneToBurnSeconds)}" +
                $",effective_done_to_burn={FormatSeconds(conversion.EffectiveDoneToBurnSeconds)}" +
                $",native_burn_threshold={FormatSeconds(conversion.NativeBurnThresholdSeconds)}" +
                $",effective_burn_threshold={FormatSeconds(conversion.EffectiveBurnThresholdSeconds)}");
        }

        return $"timings={string.Join("|", descriptions)}";
    }

    private static string FormatSeconds(float seconds)
    {
        return seconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string ItemName(ItemDrop? item)
    {
        return item == null
            ? "unknown"
            : Diagnostics.Flatten(item.gameObject.name);
    }

    private static bool IsStoneOven(CookingStation station)
    {
        return Utils.GetPrefabName(station.gameObject) == PrefabName;
    }
}
