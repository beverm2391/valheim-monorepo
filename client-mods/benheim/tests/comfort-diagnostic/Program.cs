using System;
using System.Collections.Generic;
using BenheimQoL.Interaction;

Expect(
    ComfortDiagnosticDecision.ForNativeCandidate(true, 2, false, false),
    ComfortDiagnosticDecision.Contributed);
Expect(
    ComfortDiagnosticDecision.ForNativeCandidate(true, 0, false, false),
    ComfortDiagnosticDecision.ContributedZero);
Expect(
    ComfortDiagnosticDecision.ForNativeCandidate(false, 2, true, false),
    ComfortDiagnosticDecision.DuplicateGroup);
Expect(
    ComfortDiagnosticDecision.ForNativeCandidate(false, 2, false, true),
    ComfortDiagnosticDecision.DuplicateName);
Expect(
    ComfortDiagnosticDecision.ForNativeCandidate(false, 2, true, true),
    ComfortDiagnosticDecision.DuplicateGroupAndName);
Expect(
    ComfortDiagnosticDecision.ForNativeCandidate(false, 2, false, false),
    ComfortDiagnosticDecision.NativeSkipUnclassified);

ComfortDiagnosticSnapshot snapshot = new ComfortDiagnosticSnapshot
{
    RadiusUsed = true,
    QueryRadius = 20f,
    InShelter = true,
    Resting = true,
    Rested = true,
    CalculatedComfort = 9,
    Candidates = new List<ComfortDiagnosticPiece>
    {
        new("hearth", "$piece_hearth", "Fire", 7.371f, 2,
            ComfortDiagnosticDecision.Contributed),
        new("hearth", "$piece_hearth", "Fire", 18.124f, 2,
            ComfortDiagnosticDecision.DuplicateGroupAndName),
        new("piece_brazierceiling01", "$piece_brazierceiling01", "Fire", 5.559f, 1,
            ComfortDiagnosticDecision.DuplicateGroup),
        new("piece_bathtub", "$piece_bathtub", "None", 19.358f, 0,
            ComfortDiagnosticDecision.ContributedZero),
    },
    RadiusExclusions = new List<ComfortDiagnosticPiece>
    {
        new("piece_table", "$piece_table", "Table", 27.201f, 1,
            ComfortDiagnosticDecision.RadiusExcluded),
    },
    RadiusExclusionCount = 3,
};

IReadOnlyList<string> lines = ComfortDiagnosticSummary.Format(
    snapshot,
    token => token switch
    {
        "$piece_hearth" => "Hearth",
        "$piece_bathtub" => "Hot tub",
        "$piece_table" => "Table",
        _ => token,
    });

ExpectLine(lines, "COMFORT 9 — sheltered, resting, rested — radius 20m");
ExpectLine(lines, "COUNTED");
ExpectLine(lines, "  Hearth +2 — 7.4m");
ExpectLine(lines, "IGNORED");
ExpectLine(lines, "  Hearth — 18.1m — duplicate Fire group and furniture");
ExpectLine(lines, "  piece brazierceiling01 — 5.6m — duplicate Fire group");
ExpectLine(lines, "  Hot tub — 19.4m — 0 comfort");
ExpectLine(lines, "JUST OUTSIDE RANGE");
ExpectLine(lines, "  Table — 27.2m");
ExpectLine(lines, "  …and 2 more outside range");
ExpectLine(lines, "Pieces that Valheim filters before its comfort query are not observable.");

Console.WriteLine("comfort diagnostic decision and readable summary checks passed");

static void Expect(string actual, string expected)
{
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void ExpectLine(IReadOnlyList<string> lines, string expected)
{
    for (int index = 0; index < lines.Count; index++)
    {
        if (string.Equals(lines[index], expected, StringComparison.Ordinal))
        {
            return;
        }
    }
    throw new InvalidOperationException($"Missing line: {expected}");
}
