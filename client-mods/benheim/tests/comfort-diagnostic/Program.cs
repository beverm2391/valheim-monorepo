using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
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

List<ComfortDiagnosticPiece> candidates = new List<ComfortDiagnosticPiece>();
for (int index = 0; index < 18; index++)
{
    bool contributed = index < 6;
    string decision = contributed
        ? index == 5
            ? ComfortDiagnosticDecision.ContributedZero
            : ComfortDiagnosticDecision.Contributed
        : ComfortDiagnosticDecision.DuplicateGroup;
    candidates.Add(Piece(
        $"candidate-{index}",
        decision));
}

List<ComfortDiagnosticPiece> reportedExclusions = new List<ComfortDiagnosticPiece>();
for (int index = 0; index < 8; index++)
{
    reportedExclusions.Add(Piece(
        $"outside-{index}",
        ComfortDiagnosticDecision.RadiusExcluded));
}

ComfortDiagnosticCapture.Snapshot = new ComfortDiagnosticSnapshot(
    radiusUsed: true,
    queryRadius: 20f,
    inShelter: true,
    resting: true,
    rested: true,
    calculatedComfort: 9,
    cachedComfort: 9,
    candidates,
    reportedExclusions,
    radiusExclusionCount: 12);
Player.m_localPlayer = new Player();

List<string> console = new List<string>();
ComfortDiagnosticCommand.Run(Array.Empty<string>(), console.Add);

Require(console.Count == 2, "dense comfort snapshots keep console output fixed at two lines");
Expect(
    console[0],
    "COMFORT 9 — 6 contributors, 12 ignored, 12 just outside range — radius 20m");
Expect(console[1], "Native prefilter: not observable.");

Require(Diagnostics.Events.Count == 27,
    "summary plus every reported candidate and radius exclusion remain typed events");
DiagnosticEvent summary = Diagnostics.Events[0];
Expect(summary.Domain, "Comfort");
Expect(summary.Name, "comfort_debug_summary");
ExpectField(summary, "calculated_comfort", 9);
ExpectField(summary, "cached_comfort", 9);
ExpectField(summary, "native_candidate_count", 18);
ExpectField(summary, "contributor_count", 6);
ExpectField(summary, "duplicate_or_native_skip_count", 12);
ExpectField(summary, "radius_exclusion_count", 12);
ExpectField(summary, "radius_exclusion_reported", 8);
ExpectField(summary, "radius_exclusion_truncated", true);
ExpectField(summary, "native_prefilter_visibility", "not_observable");

string operationId = Field<string>(summary, "operation_id");
Require(!string.IsNullOrWhiteSpace(operationId), "summary carries an operation id");
for (int index = 0; index < 18; index++)
{
    string decision = index < 6
        ? index == 5
            ? ComfortDiagnosticDecision.ContributedZero
            : ComfortDiagnosticDecision.Contributed
        : ComfortDiagnosticDecision.DuplicateGroup;
    ExpectPieceEvidence(
        Diagnostics.Events[index + 1],
        operationId,
        "native_candidate",
        index,
        $"candidate-{index}",
        decision);
}
for (int index = 0; index < 8; index++)
{
    ExpectPieceEvidence(
        Diagnostics.Events[index + 19],
        operationId,
        "nearest_radius_exclusion",
        index,
        $"outside-{index}",
        ComfortDiagnosticDecision.RadiusExcluded);
}

Console.WriteLine("comfort diagnostic bounded console and correlated evidence checks passed");

static ComfortDiagnosticPiece Piece(string identity, string decision) => new(
    identity,
    "session_only",
    identity,
    $"${identity}",
    "Fire",
    5f,
    decision == ComfortDiagnosticDecision.Contributed ? 1 : 0,
    decision,
    decision == ComfortDiagnosticDecision.DuplicateGroup,
    false);

static void ExpectPieceEvidence(
    DiagnosticEvent evidence,
    string operationId,
    string scope,
    int order,
    string identity,
    string decision)
{
    Expect(evidence.Domain, "Comfort");
    Expect(evidence.Name, "comfort_debug_piece");
    ExpectField(evidence, "operation_id", operationId);
    ExpectField(evidence, "operation_phase", "evidence");
    ExpectField(evidence, "scope", scope);
    ExpectField(evidence, "order", order);
    ExpectField(evidence, "identity", identity);
    ExpectField(evidence, "identity_scope", "session_only");
    ExpectField(evidence, "prefab", identity);
    ExpectField(evidence, "name_token", $"${identity}");
    ExpectField(evidence, "group", "Fire");
    ExpectField(evidence, "distance", 5f);
    ExpectField(
        evidence,
        "comfort",
        decision == ComfortDiagnosticDecision.Contributed ? 1 : 0);
    ExpectField(evidence, "decision", decision);
    ExpectField(
        evidence,
        "same_group_as_previous",
        decision == ComfortDiagnosticDecision.DuplicateGroup);
    ExpectField(evidence, "same_name_as_previous", false);
}

static T Field<T>(DiagnosticEvent diagnosticEvent, string name)
{
    if (diagnosticEvent.Fields.TryGetValue(name, out object? value) && value is T typed)
    {
        return typed;
    }
    throw new InvalidOperationException($"Missing or invalid field {name}.");
}

static void ExpectField<T>(DiagnosticEvent diagnosticEvent, string name, T expected)
{
    T actual = Field<T>(diagnosticEvent, name);
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
    {
        throw new InvalidOperationException(
            $"Expected field {name}={expected}, got {actual}.");
    }
}

static void Expect(string actual, string expected)
{
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
