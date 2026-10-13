using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Benheim.Resident;
using Newtonsoft.Json.Linq;

namespace BenheimQoL.GreydwarfResident;

/// <summary>
/// The small, explicit data contract shared by the game request and its parser.
/// Keep it independent of Unity so its exact wire behavior can be exercised
/// without starting Valheim or contacting OpenRouter.
/// </summary>
internal sealed class ResidentSpeechContext
{
    internal string Event { get; set; } = "approach";
    internal string VisitorName { get; set; } = string.Empty;
    internal string DayPart { get; set; } = string.Empty;
    internal string Weather { get; set; } = string.Empty;
    internal string Biome { get; set; } = string.Empty;
    internal bool Wet { get; set; }
    internal bool Cold { get; set; }
    internal bool TubBurning { get; set; }
    internal bool PlayerSeated { get; set; }
    internal ResidentConversationSnapshot Conversation { get; set; } = new(0, string.Empty, Array.Empty<ResidentConversationTurn>());
    internal JObject PlayerContext { get; set; } = new();
}

internal sealed class ResidentSpeechRemark
{
    internal string Memory { get; set; } = string.Empty;
    internal bool Speak { get; set; }
    internal string Text { get; set; } = string.Empty;
}

internal sealed class ResidentSpeechProviderResponse
{
    internal string Model { get; set; } = ResidentSpeechContract.Model;
    internal Dictionary<string, double> Usage { get; } = new(StringComparer.Ordinal);
    internal string? ContentExcerpt { get; set; }
    internal bool ContentTruncated { get; set; }
    internal JObject? ParsedReply { get; set; }
    internal ResidentSpeechRemark? Remark { get; set; }
    internal string? FailureReason { get; set; }
}

internal sealed class ResidentSpeechContractException : Exception
{
    internal ResidentSpeechContractException(string reason) : base(reason) => Reason = reason;
    internal string Reason { get; }
}

internal static class ResidentSpeechContract
{
    internal const string Model = "google/gemini-3.5-flash-lite";
    internal const int MaxRemarkCharacters = 140;
    internal const int MaxTraceContentCharacters = 4096;

    private static readonly string[] ContextFields =
    {
        "event", "visitorName", "dayPart", "weather", "biome", "wet", "cold",
        "tubBurning", "playerSeated", "conversation", "playerContext"
    };

    private static readonly string[] UsageFields =
    {
        "prompt_tokens", "completion_tokens", "total_tokens", "cost"
    };

    internal static ResidentSpeechContext CreateContext(
        string visitorName,
        string dayPart,
        string weather,
        string biome,
        bool wet,
        bool cold,
        bool tubBurning,
        bool playerSeated,
        ResidentConversationSnapshot conversation,
        string eventName = "approach",
        JObject? playerContext = null)
    {
        var context = new ResidentSpeechContext
        {
            Event = eventName,
            VisitorName = visitorName,
            DayPart = dayPart,
            Weather = weather,
            Biome = biome,
            Wet = wet,
            Cold = cold,
            TubBurning = tubBurning,
            PlayerSeated = playerSeated,
            Conversation = conversation,
            PlayerContext = playerContext ?? new JObject()
        };
        ValidateContext(context);
        return context;
    }

    internal static JObject ContextJson(ResidentSpeechContext context)
    {
        ValidateContext(context);
        return new JObject
        {
            ["event"] = context.Event,
            ["visitorName"] = context.VisitorName,
            ["dayPart"] = context.DayPart,
            ["weather"] = context.Weather,
            ["biome"] = context.Biome,
            ["wet"] = context.Wet,
            ["cold"] = context.Cold,
            ["tubBurning"] = context.TubBurning,
            ["playerSeated"] = context.PlayerSeated,
            ["conversation"] = JObject.Parse(context.Conversation.ToJson()),
            ["playerContext"] = context.PlayerContext.DeepClone()
        };
    }

    internal static ResidentSpeechContext ParseContext(JToken? token)
    {
        if (token is not JObject value || value.Properties().Count() != ContextFields.Length ||
            ContextFields.Any(field => value.Property(field, StringComparison.Ordinal) is null))
            throw new ResidentSpeechContractException("invalid_context");

        if (!TryString(value["event"], out string eventName) || !ValidEvent(eventName) ||
            !TryString(value["visitorName"], out string visitorName) || !ValidContextString(visitorName, 64) ||
            !TryString(value["dayPart"], out string dayPart) ||
            !new[] { "morning", "day", "evening", "night" }.Contains(dayPart, StringComparer.Ordinal) ||
            !TryString(value["weather"], out string weather) || !ValidContextString(weather, 64) ||
            !TryString(value["biome"], out string biome) || !ValidContextString(biome, 64) ||
            !TryBoolean(value["wet"], out bool wet) ||
            !TryBoolean(value["cold"], out bool cold) ||
            !TryBoolean(value["tubBurning"], out bool tubBurning) ||
            !TryBoolean(value["playerSeated"], out bool playerSeated) ||
            value["conversation"] is not JObject || value["playerContext"] is not JObject playerContext ||
            playerContext.ToString(Formatting.None).Length > 16384)
            throw new ResidentSpeechContractException("invalid_context");

        ResidentConversationSnapshot conversation;
        try { conversation = ResidentConversationSnapshot.FromJson(value["conversation"]!.ToString(Formatting.None)); }
        catch { throw new ResidentSpeechContractException("invalid_context"); }

        return new ResidentSpeechContext
        {
            Event = eventName,
            VisitorName = visitorName,
            DayPart = dayPart,
            Weather = weather,
            Biome = biome,
            Wet = wet,
            Cold = cold,
            TubBurning = tubBurning,
            PlayerSeated = playerSeated,
            Conversation = conversation,
            PlayerContext = (JObject)playerContext.DeepClone()
        };
    }

    // The character seed is shared with Lab; each runtime owns its output cap.
    internal static string BuildPrompt(string seed)
        => seed.TrimEnd() + $"\nAt most {MaxRemarkCharacters} characters.";

    internal static JObject BuildRequest(ResidentSpeechContext context, string prompt)
    {
        if (prompt is null) throw new ArgumentNullException(nameof(prompt));
        JObject contextJson = ContextJson(context);
        JObject schema = new()
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = new JArray("speak", "text"),
            ["properties"] = new JObject
            {
                ["speak"] = new JObject { ["type"] = "boolean" },
                ["text"] = new JObject { ["type"] = "string" }
            }
        };

        bool compact = context.Conversation.CompactCount > 0;
        if (compact)
        {
            ((JArray)schema["required"]!).Add("memory");
            ((JObject)schema["properties"]!)["memory"] = new JObject { ["type"] = "string" };
            prompt += "\nFor this reply also return memory: an out-of-character factual continuity summary of the existing memory and the first " +
                context.Conversation.CompactCount + " prior conversation turns. Preserve concrete named facts and relationships; omit George's personality description. " +
                "Summarize only exchanges before the compaction boundary marker below, not newer turns or the current scene. Memory may be at most " + ResidentConversationSnapshot.MaximumSummaryCharacters +
                " characters; text still has its 140-character limit.";
        }
        var messages = new JArray { new JObject { ["role"] = "system", ["content"] = prompt } };
        if (context.Conversation.Summary.Length > 0)
            messages.Add(new JObject { ["role"] = "system", ["content"] = "George's earlier conversation memory:\n" + context.Conversation.Summary });
        for (int index = 0; index < context.Conversation.Turns.Count; index++)
        {
            if (compact && index == context.Conversation.CompactCount)
                messages.Add(new JObject { ["role"] = "system", ["content"] =
                    "Compaction boundary: summarize only the older exchanges above into memory, preserving their concrete named facts. This is factual bookkeeping, not George's dialogue. Keep the following exchanges verbatim; exclude them and the current scene from memory." });
            ResidentConversationTurn turn = context.Conversation.Turns[index];
            messages.Add(new JObject { ["role"] = "user", ["content"] = new JObject
                { ["visitorName"] = turn.VisitorName, ["event"] = turn.EventName }.ToString(Formatting.None) });
            messages.Add(new JObject { ["role"] = "assistant", ["content"] = new JObject
                { ["speak"] = true, ["text"] = turn.Text }.ToString(Formatting.None) });
        }
        // The history is already the conversation above, rather than a second
        // copy inside the latest user message. The trace keeps the full snapshot.
        contextJson.Remove("conversation");
        messages.Add(new JObject { ["role"] = "user", ["content"] = contextJson.ToString(Formatting.None) });

        return new JObject
        {
            ["model"] = Model,
            ["max_tokens"] = compact ? 768 : 120,
            ["temperature"] = 0.8,
            ["response_format"] = new JObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JObject
                {
                    ["name"] = "resident_remark",
                    ["strict"] = true,
                    ["schema"] = schema
                }
            },
            ["messages"] = messages
        };
    }

    internal static ResidentSpeechProviderResponse ParseProviderResponse(string body, string requestedModel, bool requireMemory = false)
    {
        var result = new ResidentSpeechProviderResponse { Model = SafeModel(requestedModel) ? requestedModel : Model };
        JObject response;
        try
        {
            response = JObject.Parse(body);
        }
        catch
        {
            result.FailureReason = "provider_response_json";
            return result;
        }

        if (TryString(response["model"], out string actualModel) && SafeModel(actualModel))
            result.Model = actualModel;
        ReadUsage(response["usage"], result.Usage);

        if (response["choices"] is not JArray choices || choices.Count == 0 ||
            choices[0]?["message"]?["content"]?.Type != JTokenType.String)
        {
            result.FailureReason = "model_content_missing";
            return result;
        }

        string content = choices[0]!["message"]!["content"]!.Value<string>()!;
        result.ContentExcerpt = content.Length <= MaxTraceContentCharacters
            ? content
            : content.Substring(0, MaxTraceContentCharacters);
        result.ContentTruncated = content.Length > MaxTraceContentCharacters;

        JToken parsed;
        try
        {
            parsed = JToken.Parse(content);
        }
        catch
        {
            result.FailureReason = "model_reply_json";
            return result;
        }

        if (parsed is JObject parsedObject)
        {
            var safeReply = new JObject();
            safeReply["speak"] = parsedObject["speak"]?.Type == JTokenType.Boolean
                ? parsedObject["speak"]!.DeepClone()
                : JValue.CreateNull();
            safeReply["text"] = parsedObject["text"]?.Type == JTokenType.String
                ? BoundedString(parsedObject["text"]!.Value<string>()!, MaxTraceContentCharacters)
                : null;
            result.ParsedReply = safeReply;
        }

        try
        {
            result.Remark = ParseRemark(parsed, requireMemory);
        }
        catch (ResidentSpeechContractException exception)
        {
            result.FailureReason = exception.Reason;
        }
        return result;
    }

    internal static ResidentSpeechRemark ParseRemark(JToken? token, bool requireMemory = false)
    {
        if (token is not JObject value || value.Properties().Count() != (requireMemory ? 3 : 2) ||
            value.Property("speak", StringComparison.Ordinal) is null ||
            value.Property("text", StringComparison.Ordinal) is null ||
            !TryBoolean(value["speak"], out bool speak) || !TryString(value["text"], out string rawText))
            throw new ResidentSpeechContractException("invalid_remark");

        string text = rawText.Trim();
        if ((!speak && rawText.Length != 0) ||
            (speak && text.Length == 0) ||
            text.Length > MaxRemarkCharacters ||
            HasMarkupOrControl(text))
            throw new ResidentSpeechContractException("invalid_remark");

        string memory = string.Empty;
        if (requireMemory && (!TryString(value["memory"], out memory) ||
            string.IsNullOrWhiteSpace(memory) || !ResidentConversationText.IsValidSummary(memory)))
            throw new ResidentSpeechContractException("invalid_memory");
        return new ResidentSpeechRemark { Speak = speak, Text = text, Memory = memory };
    }

    private static void ValidateContext(ResidentSpeechContext context)
    {
        if (context is null || !ValidEvent(context.Event) ||
            !ValidContextString(context.VisitorName, 64) ||
            !new[] { "morning", "day", "evening", "night" }.Contains(context.DayPart, StringComparer.Ordinal) ||
            !ValidContextString(context.Weather, 64) || !ValidContextString(context.Biome, 64) ||
            context.Conversation is null || context.PlayerContext is null ||
            context.PlayerContext.ToString(Formatting.None).Length > 16384)
            throw new ResidentSpeechContractException("invalid_context");
    }

    private static bool ValidEvent(string value) => value is "approach" or "talk";

    private static bool ValidContextString(string value, int maximum) =>
        value is not null && value.Length <= maximum && !HasMarkupOrControl(value);

    private static bool HasMarkupOrControl(string value) => value.Any(character =>
        character == '<' || character == '>' || character <= '\u001f' || character == '\u007f');

    private static bool TryString(JToken? token, out string value)
    {
        if (token?.Type == JTokenType.String)
        {
            value = token.Value<string>()!;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool TryBoolean(JToken? token, out bool value)
    {
        if (token?.Type == JTokenType.Boolean)
        {
            value = token.Value<bool>();
            return true;
        }
        value = false;
        return false;
    }

    private static bool SafeModel(string value) => value.Length <= 128 && value.Length > 0 &&
        value.All(character => character is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' ||
            "_./:-".Contains(character));

    private static string BoundedString(string value, int maximum) =>
        value.Length <= maximum ? value : value.Substring(0, maximum);

    private static void ReadUsage(JToken? token, Dictionary<string, double> usage)
    {
        if (token is not JObject value) return;
        foreach (string field in UsageFields)
        {
            JToken? amount = value[field];
            if (amount?.Type != JTokenType.Integer && amount?.Type != JTokenType.Float) continue;
            try
            {
                double number = amount.Value<double>();
                if (!double.IsNaN(number) && !double.IsInfinity(number)) usage[field] = number;
            }
            catch (Exception exception) when (exception is JsonException || exception is FormatException || exception is OverflowException)
            {
                // Provider usage is optional evidence. Ignore fields outside a
                // finite JSON number rather than allowing them into local trace.
            }
        }
    }
}
