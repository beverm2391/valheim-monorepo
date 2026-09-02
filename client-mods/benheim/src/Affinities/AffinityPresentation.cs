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
    private const string LungeSuffix = " · Lunge";
    private const string LungeSectionHeading = "<color=orange>Affinity: Lunge</color>";

    internal static AffinityRequirementSpec RequirementsFor(AffinityLoadResult affinity)
    {
        if (affinity != AffinityLoadResult.Lunge)
        {
            throw new ArgumentOutOfRangeException(
                nameof(affinity),
                affinity,
                "No requirements are defined for this Affinity.");
        }

        return new AffinityRequirementSpec(
            stationNameToken: "$piece_forge",
            stationLevel: 1,
            materialPrefab: "Wood",
            materialAmount: 1);
    }

    internal static string InventoryTitle(string nativeTitle, AffinityLoadResult affinity)
    {
        if (affinity != AffinityLoadResult.Lunge
            || nativeTitle.EndsWith(LungeSuffix, StringComparison.Ordinal))
        {
            return nativeTitle;
        }

        return nativeTitle + LungeSuffix;
    }

    internal static string InventoryTooltip(
        string nativeTooltip,
        AffinityLoadResult affinity,
        float forwardImpulse,
        float minimumVerticalVelocity)
    {
        if (affinity != AffinityLoadResult.Lunge
            || nativeTooltip.IndexOf(LungeSectionHeading, StringComparison.Ordinal) >= 0)
        {
            return nativeTooltip;
        }

        return nativeTooltip.TrimEnd()
            + $"\n\n{LungeSectionHeading}\n"
            + $"Every airborne primary swing adds {forwardImpulse:0.#} m/s to your forward velocity and raises your vertical velocity to at least +{minimumVerticalVelocity:0.#} m/s. "
            + "Grounded swings are unchanged.\n"
            + "Persistent bias: Every airborne primary swing commits you to moving forward and reduces your precision and flexibility in the air.";
    }
}
