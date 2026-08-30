using System;
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

Console.WriteLine("comfort diagnostic decision checks passed");

static void Expect(string actual, string expected)
{
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
