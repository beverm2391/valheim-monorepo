using System;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx;
using Benheim.Resident;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BenheimQoL.GreydwarfResident;

/// <summary>
/// Private evidence for one contextual speech request, stored locally and
/// forwarded to the compatible server. Raw dialogue bypasses the public
/// operational diagnostics pipeline.
/// </summary>
internal static class ResidentSpeechTrace
{
    private static readonly object Sync = new();
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static string? filePath;
    private static int failureReported;
    private static int forwardFailureReported;

    internal static void Record(string requestId, string phase, object data)
    {
        try
        {
            JToken payload = data is null ? JValue.CreateNull() : JToken.FromObject(data);
            string rawJsonData = payload.ToString(Formatting.None);
            if (!ResidentSpeechTracePacket.TrySanitizeJsonData(
                    rawJsonData,
                    ResidentSpeechSettings.ApiKey,
                    out string sanitizedJsonData,
                    out string sanitizeReason))
            {
                RecordRejected(requestId, phase, sanitizeReason);
                return;
            }

            if (!ResidentSpeechTracePacket.TryValidateMetadata(
                    "client", requestId, phase, out string metadataReason))
            {
                RecordRejected(requestId, phase, metadataReason);
                return;
            }

            JToken safePayload = JToken.Parse(sanitizedJsonData);
            try
            {
                AppendLocalRecord(requestId, phase, safePayload);
            }
            catch
            {
                ReportWriteFailure();
            }

            try
            {
                // This method is implemented by the lifecycle owner and is a
                // no-op until the authenticated server connection is usable.
                ResidentClient.SendTrace(requestId, phase, sanitizedJsonData);
            }
            catch
            {
                ReportForwardFailure();
            }
        }
        catch
        {
            ReportWriteFailure();
        }
    }

    private static string CreateFilePath()
    {
        string directory = Path.Combine(Paths.ConfigPath, "Benheim", "GreydwarfResident");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "speech-trace-" + Guid.NewGuid().ToString("N") + ".jsonl");
    }

    private static void AppendLocalRecord(string requestId, string phase, JToken data)
    {
        lock (Sync)
        {
            filePath ??= CreateFilePath();
            var record = new JObject
            {
                ["utc"] = DateTime.UtcNow.ToString("O"),
                ["source"] = "game",
                ["requestId"] = requestId,
                ["phase"] = phase,
                ["data"] = data
            };
            File.AppendAllText(filePath, record.ToString(Formatting.None) + "\n", Utf8WithoutBom);
        }
    }

    private static void RecordRejected(string requestId, string phase, string reason)
    {
        try
        {
            bool validRequestId = ResidentSpeechTracePacket.TryValidateMetadata(
                "client", requestId, "trace_rejected", out _);
            var rejection = new JObject
            {
                ["rejectedPhase"] = SafeLabel(phase),
                ["reason"] = SafeLabel(reason)
            };
            try
            {
                AppendLocalRecord(validRequestId ? requestId : string.Empty, "trace_rejected", rejection);
            }
            catch
            {
                ReportWriteFailure();
            }

            if (validRequestId)
            {
                try
                {
                    ResidentClient.SendTrace(requestId, "trace_rejected", rejection.ToString(Formatting.None));
                }
                catch
                {
                    ReportForwardFailure();
                }
            }
        }
        catch
        {
            ReportWriteFailure();
        }
    }

    private static string SafeLabel(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "unknown";
        var builder = new StringBuilder(Math.Min(value.Length, ResidentSpeechTracePacket.MaximumPhaseLength));
        foreach (char character in value)
        {
            if (builder.Length >= ResidentSpeechTracePacket.MaximumPhaseLength) break;
            if ((character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') ||
                character == '_' || character == '-' || character == '.')
                builder.Append(character);
        }
        return builder.Length == 0 ? "unknown" : builder.ToString();
    }

    private static void ReportForwardFailure()
    {
        if (Interlocked.Exchange(ref forwardFailureReported, 1) != 0) return;
        try { ResidentDiagnostics.Emit("speech_trace_forward_failed", "trace_forward_failed"); }
        catch { /* Trace forwarding must never interrupt the speech operation. */ }
    }

    private static void ReportWriteFailure()
    {
        if (Interlocked.Exchange(ref failureReported, 1) != 0) return;
        try { Plugin.Log.LogWarning("[GreydwarfResident] local speech trace failed: trace_write_failed"); }
        catch { Debug.LogWarning("[GreydwarfResident] local speech trace failed: trace_write_failed"); }
        try { ResidentDiagnostics.Emit("speech_trace_failed", "trace_write_failed"); }
        catch { /* Trace failures must never interrupt the speech operation. */ }
    }
}
