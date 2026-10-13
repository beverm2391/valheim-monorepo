using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Benheim.Resident;

/// <summary>
/// Bounds and sanitizes one private speech trace packet. Trace payloads contain
/// player context and provider dialogue, so validation happens on both the
/// client before sending and the server before writing the private trace file.
/// </summary>
internal static class ResidentSpeechTracePacket
{
    internal const int MaximumSourceLength = 64;
    internal const int MaximumRequestIdLength = 64;
    internal const int MaximumPhaseLength = 64;

    // The provider response limit is 64 KiB. A JSON string containing that
    // response may need roughly twice the space for escaping, so leave margin
    // for the trace envelope while keeping each RPC bounded.
    internal const int MaximumJsonDataBytes = 192 * 1024;
    internal const int MaximumPacketBytes = MaximumJsonDataBytes + 1024;

    private const int MaximumJsonDepth = 32;
    private const int MaximumEmbeddedJsonDepth = 4;
    private const string Redacted = "[REDACTED]";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly Regex BearerCredential = new(
        "(?i)\\b(Bearer|Basic)\\s+[^\\s\"']+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CredentialAssignment = new(
        @"(?i)([""']?(?:key|[a-z0-9_-]*(?:api[_-]?key|authorization|password|secret|credential|private[_-]?key|access[_-]?token|refresh[_-]?token|bearer|auth)[a-z0-9_-]*)[""']?\s*[:=]\s*)(?:""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|(?:Bearer|Basic)\s+[^\s,}\]]+|[^\s,}\]]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ApiKeyValue = new(
        @"(?i)\bsk-[A-Za-z0-9_-]{12,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static bool TryValidateMetadata(
        string? source,
        string? requestId,
        string? phase,
        out string reason)
    {
        if (!IsSafeIdentifier(source, MaximumSourceLength, allowColon: true))
        {
            reason = "invalid_source";
            return false;
        }

        if (!IsSafeIdentifier(requestId, MaximumRequestIdLength, allowColon: false))
        {
            reason = "invalid_request_id";
            return false;
        }

        if (!IsSafeIdentifier(phase, MaximumPhaseLength, allowColon: false))
        {
            reason = "invalid_phase";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// Parses exactly one JSON value, removes credential fields and values
    /// recursively, including JSON encoded inside string fields, and rejects
    /// data that cannot fit in a single bounded RPC packet. It never truncates.
    /// </summary>
    internal static bool TrySanitizeJsonData(
        string? jsonData,
        string? knownCredential,
        out string sanitizedJsonData,
        out string reason)
    {
        sanitizedJsonData = string.Empty;
        if (string.IsNullOrEmpty(jsonData))
        {
            reason = "empty_json_data";
            return false;
        }

        if (!TryGetUtf8ByteCount(jsonData, out int inputBytes) || inputBytes > MaximumJsonDataBytes)
        {
            reason = "json_data_too_large";
            return false;
        }

        if (!TryParseJson(jsonData, out JToken? token))
        {
            reason = "invalid_json_data";
            return false;
        }

        try
        {
            SanitizeToken(token!, knownCredential, embeddedJsonDepth: 0);
            sanitizedJsonData = token!.ToString(Formatting.None);
        }
        catch
        {
            sanitizedJsonData = string.Empty;
            reason = "invalid_json_data";
            return false;
        }

        if (!TryGetUtf8ByteCount(sanitizedJsonData, out int outputBytes) ||
            outputBytes > MaximumJsonDataBytes)
        {
            sanitizedJsonData = string.Empty;
            reason = "json_data_too_large";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsSafeIdentifier(string? value, int maximumLength, bool allowColon)
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

    private static bool TryGetUtf8ByteCount(string value, out int byteCount)
    {
        try
        {
            byteCount = StrictUtf8.GetByteCount(value);
            return true;
        }
        catch
        {
            byteCount = 0;
            return false;
        }
    }

    private static bool TryParseJson(string json, out JToken? token)
    {
        token = null;
        try
        {
            using var stringReader = new StringReader(json);
            using var jsonReader = new JsonTextReader(stringReader)
            {
                DateParseHandling = DateParseHandling.None,
                MaxDepth = MaximumJsonDepth,
                SupportMultipleContent = false
            };

            if (!jsonReader.Read() || jsonReader.TokenType == JsonToken.Comment) return false;
            token = JToken.Load(jsonReader);
            while (jsonReader.Read())
            {
                if (jsonReader.TokenType != JsonToken.Comment) return false;
            }

            return true;
        }
        catch
        {
            token = null;
            return false;
        }
    }

    private static void SanitizeToken(JToken token, string? knownCredential, int embeddedJsonDepth)
    {
        if (token is JObject obj)
        {
            foreach (JProperty property in obj.Properties())
            {
                if (IsCredentialName(property.Name))
                {
                    property.Value = Redacted;
                }
                else
                {
                    SanitizeToken(property.Value, knownCredential, embeddedJsonDepth);
                }
            }

            return;
        }

        if (token is JArray array)
        {
            foreach (JToken child in array)
                SanitizeToken(child, knownCredential, embeddedJsonDepth);
            return;
        }

        if (token is not JValue value || value.Type != JTokenType.String) return;
        string text = value.Value<string>() ?? string.Empty;
        if (!string.IsNullOrEmpty(knownCredential))
            text = text.Replace(knownCredential, Redacted, StringComparison.Ordinal);
        text = CredentialAssignment.Replace(text, "$1\"" + Redacted + "\"");
        text = BearerCredential.Replace(text, "$1 " + Redacted);
        text = ApiKeyValue.Replace(text, Redacted);

        if (embeddedJsonDepth < MaximumEmbeddedJsonDepth && TryParseEmbeddedJson(text, out JToken? embedded))
        {
            SanitizeToken(embedded!, knownCredential, embeddedJsonDepth + 1);
            text = embedded!.ToString(Formatting.None);
        }

        value.Value = text;
    }

    private static bool TryParseEmbeddedJson(string text, out JToken? token)
    {
        token = null;
        string trimmed = text.TrimStart();
        if (trimmed.Length < 2 || (trimmed[0] != '{' && trimmed[0] != '[')) return false;
        return TryParseJson(trimmed, out token);
    }

    private static bool IsCredentialName(string name)
    {
        var normalized = new StringBuilder(name.Length);
        foreach (char character in name)
        {
            if (character >= 'A' && character <= 'Z')
                normalized.Append((char)(character + ('a' - 'A')));
            else if ((character >= 'a' && character <= 'z') || (character >= '0' && character <= '9'))
                normalized.Append(character);
        }

        string key = normalized.ToString();
        return key == "key" || key == "auth" || key == "bearer" ||
            key.Contains("apikey", StringComparison.Ordinal) ||
            key.Contains("authorization", StringComparison.Ordinal) ||
            key.Contains("password", StringComparison.Ordinal) ||
            key.Contains("secret", StringComparison.Ordinal) ||
            key.Contains("credential", StringComparison.Ordinal) ||
            key.Contains("privatekey", StringComparison.Ordinal) ||
            (key.Contains("token", StringComparison.Ordinal) &&
                !IsTokenUsageCounter(key));
    }

    private static bool IsTokenUsageCounter(string key) =>
        key == "prompttokens" || key == "completiontokens" || key == "totaltokens" ||
        key == "inputtokens" || key == "outputtokens" || key == "reasoningtokens" ||
        key == "cachedtokens" || key == "cachecreationinputtokens" || key == "cachereadinputtokens";
}
