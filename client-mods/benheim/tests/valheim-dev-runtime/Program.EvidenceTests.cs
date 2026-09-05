using System;
using System.Text.Json;
using System.Threading.Tasks;
using BenheimQoL.Infrastructure;
using BenheimQoL.ValheimDev;

internal static partial class Program
{
    private static void EvidenceBoundaries(string inspectionAssembly)
    {
        Task<string> countOverflow = StartObservedInspection(
            "count-overflow", inspectionAssembly, "Test:overflow");
        for (int index = 0; index < ValheimDevProtocol.MaximumEvidenceEvents * 3; index++)
        {
            Diagnostics.Emit(DiagnosticEvent.Create("Test", "overflow").String("index", index.ToString()));
        }
        JsonElement countEvidence = Parse(Pump(countOverflow));
        Require(countEvidence.GetProperty("evidence_events").GetArrayLength() == ValheimDevProtocol.MaximumEvidenceEvents
            && countEvidence.GetProperty("evidence_truncated").GetBoolean()
            && countEvidence.GetProperty("dropped_evidence_events").GetInt32() > 0,
            "evidence count overflow is explicit and measured");

        Task<string> byteOverflow = StartObservedInspection(
            "byte-overflow", inspectionAssembly, "Test:large");
        for (int index = 0; index < 40; index++)
        {
            Diagnostics.Emit(DiagnosticEvent.Create("Test", "large").String("blob", new string('x', 10000)));
        }
        JsonElement byteEvidence = Parse(Pump(byteOverflow));
        Require(byteEvidence.GetProperty("evidence_events").GetArrayLength() < 40
            && byteEvidence.GetProperty("evidence_truncated").GetBoolean()
            && byteEvidence.GetProperty("dropped_evidence_events").GetInt32() > 0,
            "evidence byte overflow is explicit and measured");

        Task<string> exactBoundary = StartObservedInspection(
            "exact-byte-boundary", inspectionAssembly, "Test:exact");
        DiagnosticEvent empty = DiagnosticEvent.Create("Test", "exact").String("blob", string.Empty);
        int fixedBytes = 2 + ValheimDevJson.EncodedStringUtf8ByteCount(empty.ToJsonLine());
        DiagnosticEvent exact = DiagnosticEvent.Create("Test", "exact")
            .String("blob", new string('x', ValheimDevProtocol.MaximumEvidenceBytes - fixedBytes));
        Require(2 + ValheimDevJson.EncodedStringUtf8ByteCount(exact.ToJsonLine())
                == ValheimDevProtocol.MaximumEvidenceBytes,
            "evidence fixture reaches the exact serialized-array byte boundary");
        Diagnostics.Emit(exact);
        JsonElement exactEvidence = Parse(Pump(exactBoundary));
        Require(exactEvidence.GetProperty("evidence_events").GetArrayLength() == 1
            && !exactEvidence.GetProperty("evidence_truncated").GetBoolean()
            && exactEvidence.GetProperty("dropped_evidence_events").GetInt32() == 0,
            "the exact wire-size evidence boundary is accepted without truncation");

        Task<string> first = StartObservedInspection(
            "observer-a", inspectionAssembly, "Test:finish-a");
        Action<DiagnosticEvent> delayedObserver = Diagnostics.CaptureObserverForTests()
            ?? throw new InvalidOperationException("operation A observer was not installed");
        Diagnostics.Emit(DiagnosticEvent.Create("Test", "finish-a"));
        Require(Parse(Pump(first)).GetProperty("ok").GetBoolean(), "operation A completes before delayed delivery");

        Task<string> second = StartObservedInspection(
            "observer-b", inspectionAssembly, "Test:cross");
        delayedObserver(DiagnosticEvent.Create("Test", "cross").String("origin", "delayed-a"));
        ValheimDevRuntime.Update();
        Require(!second.IsCompleted, "operation A's delayed observer cannot satisfy operation B");
        Diagnostics.Emit(DiagnosticEvent.Create("Test", "cross").String("origin", "current-b"));
        JsonElement secondEvidence = Parse(Pump(second));
        string captured = secondEvidence.GetProperty("evidence_events")[0].GetString()!;
        Require(secondEvidence.GetProperty("evidence_events").GetArrayLength() == 1
            && captured.Contains("current-b", StringComparison.Ordinal)
            && !captured.Contains("delayed-a", StringComparison.Ordinal),
            "evidence callbacks stay bound to the operation that registered them");
    }

    private static Task<string> StartObservedInspection(
        string operationId,
        string inspectionAssembly,
        string selector)
    {
        Task<string> task = SendAsync(CodeRequest(
            "inspect", operationId, string.Empty, inspectionAssembly, new[] { selector }, 5000));
        WaitForQueue();
        ValheimDevRuntime.Update();
        Require(!task.IsCompleted, operationId + " waits for selected evidence");
        return task;
    }
}
