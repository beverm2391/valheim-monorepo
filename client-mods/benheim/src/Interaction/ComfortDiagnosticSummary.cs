using System.Collections.Generic;

namespace BenheimQoL.Interaction;

internal static class ComfortDiagnosticSummary
{
    internal static IReadOnlyList<string> Format(
        ComfortDiagnosticSnapshot snapshot,
        int contributorCount)
    {
        int ignoredCount = snapshot.Candidates.Count - contributorCount;
        string radius = snapshot.RadiusUsed
            ? $"{snapshot.QueryRadius:0.##}m"
            : "not used";
        return new List<string>
        {
            $"COMFORT {snapshot.CalculatedComfort} — " +
            $"{contributorCount} contributors, {ignoredCount} ignored, " +
            $"{snapshot.RadiusExclusionCount} just outside range — radius {radius}",
            "Native prefilter: not observable.",
        };
    }
}
