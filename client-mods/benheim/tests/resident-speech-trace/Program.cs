using System;
using System.IO;
using Benheim.Resident;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

const string credential = "sk-or-v1-testcredential-1234567890";
var parsedReply = new JObject
{
    ["speak"] = true,
    ["text"] = "The rain is kind to the garden.",
    ["access_token"] = credential
};
var providerResponse = new JObject
{
    ["choices"] = new JArray
    {
        new JObject
        {
            ["message"] = new JObject
            {
                ["content"] = parsedReply.ToString(Formatting.None)
            }
        }
    },
    ["api_key"] = credential
};
var requestBody = new JObject
{
    ["model"] = "google/gemini-3.5-flash-lite",
    ["messages"] = new JArray { new JObject { ["role"] = "system", ["content"] = "Speak as George." } },
    ["headers"] = new JObject { ["Authorization"] = "Bearer " + credential }
};
var traceData = new JObject
{
    ["prompt"] = "Speak as George.",
    ["body"] = requestBody.ToString(Formatting.None),
    ["rawResponse"] = providerResponse.ToString(Formatting.None),
    ["apiKey"] = credential,
    ["tokens"] = credential,
    ["usage"] = new JObject { ["prompt_tokens"] = 17, ["completion_tokens"] = 8 },
    ["durationMs"] = 251.5
};

Expect(ResidentSpeechTracePacket.TryValidateMetadata("client:42", "request_01", "provider_raw_response", out _),
    "valid trace metadata accepted");
Expect(!ResidentSpeechTracePacket.TryValidateMetadata("client\n42", "request_01", "provider_raw_response", out string sourceReason) &&
    sourceReason == "invalid_source", "metadata rejects newline injection in source");
Expect(!ResidentSpeechTracePacket.TryValidateMetadata("client", "request\n01", "provider_raw_response", out string requestReason) &&
    requestReason == "invalid_request_id", "metadata rejects newline injection in request id");
Expect(!ResidentSpeechTracePacket.TryValidateMetadata("client", "request_01", new string('x', ResidentSpeechTracePacket.MaximumPhaseLength + 1), out string phaseReason) &&
    phaseReason == "invalid_phase", "metadata rejects overlong phases");

Expect(ResidentSpeechTracePacket.TrySanitizeJsonData(
        traceData.ToString(Formatting.None), credential, out string safeJson, out string sanitizeReason),
    "well-formed trace JSON sanitizes: " + sanitizeReason);
Expect(!safeJson.Contains(credential, StringComparison.Ordinal), "configured credential is removed everywhere");
Expect(safeJson.Contains("The rain is kind to the garden.", StringComparison.Ordinal),
    "sanitization preserves the full model reply");

JObject safe = JObject.Parse(safeJson);
Expect(safe["apiKey"]!.Value<string>() == "[REDACTED]", "credential-named root field is redacted");
Expect(safe["tokens"]!.Value<string>() == "[REDACTED]", "plural credential fields are redacted");
Expect(safe["usage"]!["prompt_tokens"]!.Value<int>() == 17 && safe["usage"]!["completion_tokens"]!.Value<int>() == 8,
    "nonsecret token usage counts are preserved");
JObject safeBody = JObject.Parse(safe["body"]!.Value<string>()!);
Expect(safeBody["headers"]!["Authorization"]!.Value<string>() == "[REDACTED]",
    "credentials nested in JSON strings are redacted");
Expect(safeBody["messages"]![0]!["content"]!.Value<string>() == "Speak as George.",
    "full prompt nested in serialized request body is retained");
JObject safeResponse = JObject.Parse(safe["rawResponse"]!.Value<string>()!);
Expect(safeResponse["api_key"]!.Value<string>() == "[REDACTED]",
    "credential fields in serialized provider response are redacted");
string safeModelContent = safeResponse["choices"]![0]!["message"]!["content"]!.Value<string>()!;
Expect(JObject.Parse(safeModelContent)["access_token"]!.Value<string>() == "[REDACTED]",
    "JSON reply embedded inside provider response is recursively redacted");

Expect(!ResidentSpeechTracePacket.TrySanitizeJsonData("{broken", null, out string malformed, out string malformedReason) &&
    malformedReason == "invalid_json_data" && malformed.Length == 0,
    "malformed outer packets are explicitly rejected without partial data");
Expect(!ResidentSpeechTracePacket.TrySanitizeJsonData("{} {}", null, out _, out string extraReason) &&
    extraReason == "invalid_json_data", "multiple top-level JSON values are rejected");
string malformedEmbedded = new JObject
{
    ["rawResponse"] = "{\"api_key\":\"fixture-malformed-response-key\""
}.ToString(Formatting.None);
Expect(ResidentSpeechTracePacket.TrySanitizeJsonData(
        malformedEmbedded, null, out string safeMalformedEmbedded, out _),
    "malformed raw response remains available for diagnosis");
Expect(!safeMalformedEmbedded.Contains("fixture-malformed-response-key", StringComparison.Ordinal),
    "credentials in malformed embedded response JSON are redacted");

string oversized = "\"" + new string('x', ResidentSpeechTracePacket.MaximumJsonDataBytes) + "\"";
Expect(!ResidentSpeechTracePacket.TrySanitizeJsonData(oversized, null, out string rejectedOversized, out string sizeReason) &&
    sizeReason == "json_data_too_large" && rejectedOversized.Length == 0,
    "oversized trace data is rejected whole rather than truncated");
string multibyteOversized = "\"" + new string('é', ResidentSpeechTracePacket.MaximumJsonDataBytes / 2) + "\"";
Expect(!ResidentSpeechTracePacket.TrySanitizeJsonData(multibyteOversized, null, out _, out string utf8SizeReason) &&
    utf8SizeReason == "json_data_too_large", "payload limits count UTF-8 bytes rather than UTF-16 characters");

string serverTraceData = new JObject
{
    ["reply"] = "The rain is kind to the garden.",
    ["headerText"] = "Bearer fixture-header-credential",
    ["nested"] = new JObject { ["access_token"] = "fixture-access-token" }
}.ToString(Formatting.None);
try
{
    Expect(BenheimServerSupport.ResidentServerTrace.Record(
            "client:42", "request_01", "final_outcome", serverTraceData),
        "server trace persists valid sanitized stages");
    string traceDirectory = Path.Combine(BepInEx.Paths.ConfigPath, "Benheim", "GreydwarfResident");
    string[] files = Directory.GetFiles(traceDirectory, "speech-trace-server-*.jsonl");
    Expect(files.Length == 1, "server trace uses its private config directory");
    string[] records = File.ReadAllLines(files[0]);
    Expect(records.Length == 1, "one valid call writes one trace record");
    Expect(!records[0].Contains("fixture-header-credential", StringComparison.Ordinal) &&
        !records[0].Contains("fixture-access-token", StringComparison.Ordinal),
        "server independently removes credentials before disk persistence");
    Expect(records[0].Contains("The rain is kind to the garden.", StringComparison.Ordinal),
        "server trace preserves full dialogue content");

    Expect(!BenheimServerSupport.ResidentServerTrace.Record(
            "client:42", "request_01", "bad_packet", "{malformed-payload"),
        "server rejects malformed trace data");
    records = File.ReadAllLines(files[0]);
    Expect(records.Length == 2 && records[1].Contains("trace_rejected", StringComparison.Ordinal) &&
        !records[1].Contains("malformed-payload", StringComparison.Ordinal),
        "server records safe rejection metadata without the rejected body");

    // Make the actual writer fail by replacing its file with a directory.
    // A failure must be queryable without emitting private dialogue, and
    // restoring the filesystem must allow later trace records to succeed.
    File.Delete(files[0]);
    Directory.CreateDirectory(files[0]);
    for (int attempt = 0; attempt < 2; attempt++)
        Expect(!BenheimServerSupport.ResidentServerTrace.Record(
            "client:42", "request_01", "final_outcome", serverTraceData),
            "disk failure returns without throwing into gameplay");
    Expect(BenheimServerSupport.ServerDiagnostics.Events.Count == 1,
        "disk failure emits one operational diagnostic without repeated spam");
    Expect(BenheimServerSupport.ServerDiagnostics.Events[0].ToReadableLine() ==
        "[diag][GreydwarfResident] speech_trace_failed reason=trace_write_failed",
        "operational failure diagnostic contains metadata only");
    Directory.Delete(files[0]);
    Expect(BenheimServerSupport.ResidentServerTrace.Record(
        "client:42", "request_01", "final_outcome", serverTraceData),
        "trace writes recover after a filesystem failure");
}
finally
{
    if (Directory.Exists(BepInEx.Paths.ConfigPath))
        Directory.Delete(BepInEx.Paths.ConfigPath, recursive: true);
}

Console.WriteLine("resident speech trace packet checks passed");

static void Expect(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
