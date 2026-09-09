using System;
using System.Text.Json;
using System.Threading.Tasks;
using ValheimDev;

internal static partial class Program
{
    private static void EvidenceBoundaries(string inspectionAssembly)
    {
        ValheimDevDiagnostics.SetEvidenceSubscriptionForTests(_ => "optional_provider_absent");
        JsonElement unavailable = Parse(Pump(SendAsync(CodeRequest(
            "run_once", "optional-evidence-absent", string.Empty, inspectionAssembly,
            new[] { "BenheimFeature:event" }, 5000))));
        Require(unavailable.GetProperty("ok").GetBoolean()
            && !unavailable.GetProperty("evidence_available").GetBoolean()
            && unavailable.GetProperty("evidence_unavailable_reason").GetString() == "optional_provider_absent",
            "missing optional Benheim evidence is explicit and does not disable general code execution");
        ValheimDevDiagnostics.SetEvidenceSubscriptionForTests(_ => "available");

        Task<string> countOverflow = StartObservedInspection(
            "count-overflow", inspectionAssembly, "Test:overflow");
        for (int index = 0; index < ValheimDevProtocol.MaximumEvidenceEvents * 3; index++)
        {
            ValheimDevDiagnostics.PublishEvidenceForTests(
                "Test", "overflow", EvidenceJson("overflow", "index", index.ToString()));
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
            ValheimDevDiagnostics.PublishEvidenceForTests(
                "Test", "large", EvidenceJson("large", "blob", new string('x', 10000)));
        }
        JsonElement byteEvidence = Parse(Pump(byteOverflow));
        Require(byteEvidence.GetProperty("evidence_events").GetArrayLength() < 40
            && byteEvidence.GetProperty("evidence_truncated").GetBoolean()
            && byteEvidence.GetProperty("dropped_evidence_events").GetInt32() > 0,
            "evidence byte overflow is explicit and measured");

        Task<string> exactBoundary = StartObservedInspection(
            "exact-byte-boundary", inspectionAssembly, "Test:exact");
        string empty = EvidenceJson("exact", "blob", string.Empty);
        int fixedBytes = 2 + ValheimDevJson.EncodedStringUtf8ByteCount(empty);
        string exact = EvidenceJson(
            "exact", "blob", new string('x', ValheimDevProtocol.MaximumEvidenceBytes - fixedBytes));
        Require(2 + ValheimDevJson.EncodedStringUtf8ByteCount(exact)
                == ValheimDevProtocol.MaximumEvidenceBytes,
            "evidence fixture reaches the exact serialized-array byte boundary");
        Action<ValheimDevEvidenceRecord> exactObserver = ValheimDevDiagnostics.CaptureObserverForTests()
            ?? throw new InvalidOperationException("exact boundary observer was not installed");
        exactObserver(new ValheimDevEvidenceRecord { Domain = "Test", Name = "exact", Json = exact });
        JsonElement exactEvidence = Parse(Pump(exactBoundary));
        Require(exactEvidence.GetProperty("evidence_events").GetArrayLength() == 1
            && !exactEvidence.GetProperty("evidence_truncated").GetBoolean()
            && exactEvidence.GetProperty("dropped_evidence_events").GetInt32() == 0,
            "the exact wire-size evidence boundary is accepted without truncation");

        Task<string> first = StartObservedInspection(
            "observer-a", inspectionAssembly, "Test:finish-a");
        Action<ValheimDevEvidenceRecord> delayedObserver = ValheimDevDiagnostics.CaptureObserverForTests()
            ?? throw new InvalidOperationException("operation A observer was not installed");
        ValheimDevDiagnostics.PublishEvidenceForTests(
            "Test", "finish-a", EvidenceJson("finish-a", "origin", "current-a"));
        Require(Parse(Pump(first)).GetProperty("ok").GetBoolean(), "operation A completes before delayed delivery");

        Task<string> second = StartObservedInspection(
            "observer-b", inspectionAssembly, "Test:cross");
        delayedObserver(new ValheimDevEvidenceRecord
        {
            Domain = "Test",
            Name = "cross",
            Json = EvidenceJson("cross", "origin", "delayed-a")
        });
        ValheimDevRuntime.Update();
        Require(!second.IsCompleted, "operation A's delayed observer cannot satisfy operation B");
        ValheimDevDiagnostics.PublishEvidenceForTests(
            "Test", "cross", EvidenceJson("cross", "origin", "current-b"));
        JsonElement secondEvidence = Parse(Pump(second));
        string captured = secondEvidence.GetProperty("evidence_events")[0].GetString()!;
        Require(secondEvidence.GetProperty("evidence_events").GetArrayLength() == 1
            && captured.Contains("current-b", StringComparison.Ordinal)
            && !captured.Contains("delayed-a", StringComparison.Ordinal),
            "evidence callbacks stay bound to the operation that registered them");
    }

    private static string EvidenceJson(string eventName, string fieldName, string value)
    {
        return JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, string>
        {
            ["domain"] = "Test",
            ["event"] = eventName,
            [fieldName] = value
        });
    }

    private static Task<string> StartObservedInspection(
        string operationId,
        string inspectionAssembly,
        string selector)
    {
        Task<string> task = SendAsync(CodeRequest(
            "run_once", operationId, string.Empty, inspectionAssembly, new[] { selector }, 5000));
        WaitForQueue();
        ValheimDevRuntime.Update();
        Require(!task.IsCompleted, operationId + " waits for selected evidence");
        return task;
    }
}
