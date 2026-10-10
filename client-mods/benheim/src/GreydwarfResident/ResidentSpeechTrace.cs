using System;
using System.IO;
using System.Text;
using System.Threading;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BenheimQoL.GreydwarfResident;

/// <summary>
/// Local-only evidence for one contextual speech request. This deliberately
/// has no route through the remote diagnostics pipeline because context and
/// dialogue are player-visible text.
/// </summary>
internal static class ResidentSpeechTrace
{
    private static readonly object Sync = new();
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private static string? filePath;
    private static int failureReported;

    internal static void Record(string requestId, string phase, object data)
    {
        try
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
                    ["data"] = data is null ? JValue.CreateNull() : JToken.FromObject(data)
                };
                RedactStrings(record, ResidentSpeechSettings.ApiKey);
                File.AppendAllText(filePath, record.ToString(Formatting.None) + "\n", Utf8WithoutBom);
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

    private static void RedactStrings(JToken token, string? secret)
    {
        if (string.IsNullOrEmpty(secret)) return;
        if (token is JValue value && value.Type == JTokenType.String)
        {
            string text = value.Value<string>() ?? string.Empty;
            if (text.Contains(secret, StringComparison.Ordinal))
                value.Value = text.Replace(secret, "[REDACTED]", StringComparison.Ordinal);
            return;
        }
        foreach (JToken child in token.Children()) RedactStrings(child, secret);
    }

    private static void ReportWriteFailure()
    {
        if (Interlocked.Exchange(ref failureReported, 1) != 0) return;
        try { Plugin.Log.LogWarning("[GreydwarfResident] local speech trace failed: trace_write_failed"); }
        catch { Debug.LogWarning("[GreydwarfResident] local speech trace failed: trace_write_failed"); }
        try { ResidentDiagnostics.Emit("speech_trace_failed", "trace_write_failed"); }
        catch { /* A trace failure must not prevent the speech request from failing closed. */ }
    }
}
