using System;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx;
using Benheim.Resident;
using BenheimQoL.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BenheimServerSupport;

/// <summary>
/// Persists private resident speech evidence on the dedicated server. Trace
/// bodies include player context and provider dialogue, so this writer never
/// forwards payloads to the ordinary diagnostics or logging paths.
/// </summary>
internal static class ResidentServerTrace
{
    private static readonly object Sync = new();
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static string? filePath;
    private static int writeFailureReported;

    /// <summary>
    /// Validates, sanitizes, and writes one trace stage. The return value lets
    /// the RPC owner explicitly reject a packet without allowing trace errors
    /// to escape into gameplay.
    /// </summary>
    internal static bool Record(string source, string requestId, string phase, string jsonData)
    {
        if (!ResidentSpeechTracePacket.TryValidateMetadata(source, requestId, phase, out string metadataReason))
        {
            RecordRejected(source, metadataReason, requestId);
            return false;
        }

        if (!ResidentSpeechTracePacket.TrySanitizeJsonData(
                jsonData, knownCredential: null, out string sanitizedJsonData, out string dataReason))
        {
            RecordRejected(source, dataReason, requestId);
            return false;
        }

        try
        {
            JToken data = JToken.Parse(sanitizedJsonData);
            var record = new JObject
            {
                ["utc"] = DateTime.UtcNow.ToString("O"),
                ["source"] = source,
                ["requestId"] = requestId,
                ["phase"] = phase,
                ["data"] = data
            };
            return Append(record);
        }
        catch
        {
            RecordRejected(source, "trace_write_failed", requestId);
            return false;
        }
    }

    /// <summary>
    /// Writes only safe rejection metadata when the RPC envelope itself could
    /// not be decoded or exceeded its packet bound. The original payload is
    /// deliberately discarded.
    /// </summary>
    internal static void RecordRejected(string? source, string reason, string? requestId = null)
    {
        string safeSource = IsSafeLabel(source, ResidentSpeechTracePacket.MaximumSourceLength, allowColon: true)
            ? source!
            : "unknown";
        string? safeRequestId = IsSafeLabel(requestId, ResidentSpeechTracePacket.MaximumRequestIdLength, allowColon: false)
            ? requestId
            : null;
        string safeReason = SafeLabel(reason);

        try
        {
            var record = new JObject
            {
                ["utc"] = DateTime.UtcNow.ToString("O"),
                ["source"] = safeSource,
                ["requestId"] = safeRequestId,
                ["phase"] = "trace_rejected",
                ["data"] = new JObject { ["reason"] = safeReason }
            };
            Append(record);
        }
        catch
        {
            // Private trace I/O is best effort. Do not leak payloads or let a
            // trace disk failure affect encounter handling.
        }
    }

    private static bool Append(JObject record)
    {
        try
        {
            lock (Sync)
            {
                filePath ??= CreateFilePath();
                File.AppendAllText(filePath, record.ToString(Formatting.None) + "\n", Utf8WithoutBom);
            }

            return true;
        }
        catch
        {
            ReportWriteFailure();
            return false;
        }
    }

    private static void ReportWriteFailure()
    {
        if (Interlocked.Exchange(ref writeFailureReported, 1) != 0) return;
        try
        {
            ServerDiagnostics.Emit(DiagnosticEvent.Create("GreydwarfResident", "speech_trace_failed")
                .String("reason", "trace_write_failed"));
        }
        catch { /* Diagnostics cannot turn a trace failure into a gameplay failure. */ }
    }

    private static string CreateFilePath()
    {
        string directory = Path.Combine(Paths.ConfigPath, "Benheim", "GreydwarfResident");
        Directory.CreateDirectory(directory);
        return Path.Combine(
            directory,
            "speech-trace-server-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N") + ".jsonl");
    }

    private static bool IsSafeLabel(string? value, int maximumLength, bool allowColon)
    {
        if (string.IsNullOrEmpty(value) || value.Length > maximumLength) return false;
        foreach (char character in value)
        {
            if ((character >= 'a' && character <= 'z') ||
                (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9') ||
                character == '_' || character == '-' || character == '.' ||
                (allowColon && character == ':'))
            {
                continue;
            }

            return false;
        }

        return true;
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
}
