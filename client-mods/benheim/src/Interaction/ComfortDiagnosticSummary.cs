using System;
using System.Collections.Generic;

namespace BenheimQoL.Interaction;

internal static class ComfortDiagnosticSummary
{
    internal static IReadOnlyList<string> Format(
        ComfortDiagnosticSnapshot snapshot,
        Func<string, string> localize)
    {
        List<string> lines = new List<string>();
        string radius = snapshot.RadiusUsed
            ? $"{snapshot.QueryRadius:0.##}m"
            : "not used";
        lines.Add(
            $"COMFORT {snapshot.CalculatedComfort} — " +
            $"{State(snapshot.InShelter, "sheltered")}, " +
            $"{State(snapshot.Resting, "resting")}, " +
            $"{State(snapshot.Rested, "rested")} — radius {radius}");

        lines.Add(string.Empty);
        lines.Add("COUNTED");
        AppendCandidates(
            lines,
            snapshot.Candidates,
            localize,
            includeCounted: true);

        lines.Add(string.Empty);
        lines.Add("IGNORED");
        AppendCandidates(
            lines,
            snapshot.Candidates,
            localize,
            includeCounted: false);

        lines.Add(string.Empty);
        lines.Add("JUST OUTSIDE RANGE");
        if (snapshot.RadiusExclusions.Count == 0)
        {
            lines.Add("  none recorded");
        }
        else
        {
            for (int index = 0; index < snapshot.RadiusExclusions.Count; index++)
            {
                ComfortDiagnosticPiece piece = snapshot.RadiusExclusions[index];
                lines.Add($"  {Name(piece, localize)} — {piece.Distance:0.0}m");
            }
        }

        int unreported = snapshot.RadiusExclusionCount - snapshot.RadiusExclusions.Count;
        if (unreported > 0)
        {
            lines.Add($"  …and {unreported} more outside range");
        }

        lines.Add(string.Empty);
        lines.Add(
            "Pieces that Valheim filters before its comfort query are not observable.");
        return lines;
    }

    private static void AppendCandidates(
        List<string> lines,
        IReadOnlyList<ComfortDiagnosticPiece> pieces,
        Func<string, string> localize,
        bool includeCounted)
    {
        int written = 0;
        for (int index = 0; index < pieces.Count; index++)
        {
            ComfortDiagnosticPiece piece = pieces[index];
            bool counted = piece.Decision == ComfortDiagnosticDecision.Contributed;
            if (counted != includeCounted)
            {
                continue;
            }

            if (counted)
            {
                lines.Add(
                    $"  {Name(piece, localize)} +{piece.Comfort} — {piece.Distance:0.0}m");
            }
            else
            {
                lines.Add(
                    $"  {Name(piece, localize)} — {piece.Distance:0.0}m — " +
                    Reason(piece));
            }
            written++;
        }

        if (written == 0)
        {
            lines.Add("  none");
        }
    }

    private static string Name(
        ComfortDiagnosticPiece piece,
        Func<string, string> localize)
    {
        string localized = localize(piece.NameToken);
        if (!string.IsNullOrWhiteSpace(localized)
            && !string.Equals(localized, piece.NameToken, StringComparison.Ordinal)
            && !localized.StartsWith("$", StringComparison.Ordinal))
        {
            return localized;
        }
        return piece.Prefab.Replace('_', ' ');
    }

    private static string Reason(ComfortDiagnosticPiece piece)
    {
        switch (piece.Decision)
        {
            case ComfortDiagnosticDecision.ContributedZero:
                return "0 comfort";
            case ComfortDiagnosticDecision.DuplicateGroup:
                return $"duplicate {piece.Group} group";
            case ComfortDiagnosticDecision.DuplicateName:
                return "duplicate furniture";
            case ComfortDiagnosticDecision.DuplicateGroupAndName:
                return $"duplicate {piece.Group} group and furniture";
            default:
                return "skipped by Valheim";
        }
    }

    private static string State(bool active, string name)
    {
        return active ? name : $"not {name}";
    }
}
