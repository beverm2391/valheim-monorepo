using System;

namespace BenheimQoL.Affinities;

internal readonly struct AffinityRequirementSpec
{
    internal AffinityRequirementSpec(
        string stationNameToken,
        int stationLevel,
        string materialPrefab,
        int materialAmount)
    {
        StationNameToken = stationNameToken;
        StationLevel = stationLevel;
        MaterialPrefab = materialPrefab;
        MaterialAmount = materialAmount;
    }

    internal string StationNameToken { get; }
    internal int StationLevel { get; }
    internal string MaterialPrefab { get; }
    internal int MaterialAmount { get; }
}

internal static class AffinityPresentation
{
    internal const string ForgeNameToken = "$piece_forge";

    internal static string NameFor(AffinityLoadResult affinity) => affinity switch
    {
        AffinityLoadResult.Lunge => "Lunge",
        AffinityLoadResult.Snipe => "Snipe",
        _ => string.Empty,
    };

    internal static AffinityRequirementSpec RequirementsFor(AffinityLoadResult affinity)
    {
        if (affinity != AffinityLoadResult.Lunge && affinity != AffinityLoadResult.Snipe)
        {
            throw new ArgumentOutOfRangeException(
                nameof(affinity),
                affinity,
                "No requirements are defined for this Affinity.");
        }

        return new AffinityRequirementSpec(
            stationNameToken: ForgeNameToken,
            stationLevel: 1,
            materialPrefab: "Wood",
            materialAmount: 1);
    }

    internal static string InventoryTitle(string nativeTitle, AffinityLoadResult affinity)
    {
        string name = NameFor(affinity);
        string suffix = " · " + name;
        if (name.Length == 0 || nativeTitle.EndsWith(suffix, StringComparison.Ordinal))
        {
            return nativeTitle;
        }

        return nativeTitle + suffix;
    }

    internal static string InventoryTooltip(
        string nativeTooltip,
        AffinityLoadResult affinity,
        float forwardImpulse,
        float minimumVerticalVelocity)
    {
        string name = NameFor(affinity);
        string heading = $"<color=orange>Affinity: {name}</color>";
        if (name.Length == 0 || nativeTooltip.IndexOf(heading, StringComparison.Ordinal) >= 0)
        {
            return nativeTooltip;
        }

        return nativeTooltip.TrimEnd()
            + $"\n\n{heading}\n"
            + BehaviorDescription(affinity, forwardImpulse, minimumVerticalVelocity);
    }

    internal static string BehaviorDescription(
        AffinityLoadResult affinity,
        float forwardImpulse,
        float minimumVerticalVelocity)
    {
        return affinity switch
        {
            AffinityLoadResult.Lunge =>
                $"Every airborne primary swing adds {forwardImpulse:0.#} m/s to your forward velocity and raises your vertical velocity to at least +{minimumVerticalVelocity:0.#} m/s. "
                + "Grounded swings are unchanged.\n"
                + "Persistent bias: Every airborne primary swing commits you to moving forward and reduces your precision and flexibility in the air.",
            AffinityLoadResult.Snipe =>
                $"Drawing automatically gives {SnipeRules.OpticalZoom:0.#}x optical zoom, smoothly changing field of view without moving the camera. "
                + "Soft edge darkening grows with the draw while the center stays clear. Native crosshair and look sensitivity stay unchanged. Scope works even with Bow Focus or Benheim FX off; zoom and edge darkening clear almost instantly when the draw ends.\n"
                + $"Persistent bias: Full draw takes {(SnipeRules.DrawDurationMultiplier - 1f) * 100f:0.#}% longer after skill adjustment, at every range. Partial draws and stamina use stay native.\n"
                + $"Total headshot damage is {SnipeRules.NearMultiplier:0.##}x through {SnipeRules.NearDistanceMeters:0.#} m, rising linearly to {SnipeRules.CapMultiplier:0.##}x at {SnipeRules.CapDistanceMeters:0.#} m and beyond "
                + $"({(SnipeRules.NearMultiplier + SnipeRules.CapMultiplier) / 2f:0.##}x at {(SnipeRules.NearDistanceMeters + SnipeRules.CapDistanceMeters) / 2f:0.#} m). "
                + "This replaces the ordinary headshot multiplier. Full draw is not required, and fired arrows keep Snipe after switching weapons. Body shots, native WeakSpots, and ammunition effects stay unchanged.",
            _ => string.Empty,
        };
    }
}
