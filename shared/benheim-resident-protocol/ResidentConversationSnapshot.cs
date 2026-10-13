using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Benheim.Resident;

/// <summary>
/// The shared, account-free conversation context sent to a resident. Older
/// turns are summarized by the server; the remaining turns stay verbatim.
/// </summary>
internal sealed class ResidentConversationSnapshot
{
    internal const int CompactionStartTurnCount = 12;
    internal const int VerbatimTurnCount = 6;
    internal const int MaximumTurnCount = 64;
    internal const int MaximumSummaryCharacters = 1200;
    internal const int MaximumVisitorNameCharacters = 64;
    internal const int MaximumEventNameCharacters = 256;
    internal const int MaximumSpokenTextCharacters = 140;
    internal const int MaximumJsonCharacters = 64 * 1024;

    private static readonly string[] SnapshotFields =
    {
        "revision", "summary", "turns", "compactCount"
    };

    private static readonly string[] TurnFields =
    {
        "visitorName", "eventName", "text"
    };

    private readonly ReadOnlyCollection<ResidentConversationTurn> turns;

    internal ResidentConversationSnapshot(
        long revision,
        string summary,
        IEnumerable<ResidentConversationTurn> turns)
    {
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        if (!ResidentConversationText.IsValidSummary(summary))
        {
            throw new ArgumentException("Conversation summary is invalid.", nameof(summary));
        }

        if (turns == null)
        {
            throw new ArgumentNullException(nameof(turns));
        }

        var copiedTurns = new List<ResidentConversationTurn>();
        foreach (ResidentConversationTurn turn in turns)
        {
            if (turn == null || !ResidentConversationTurn.IsValid(
                    turn.VisitorName,
                    turn.EventName,
                    turn.Text))
            {
                throw new ArgumentException("Conversation turn is invalid.", nameof(turns));
            }

            if (copiedTurns.Count == MaximumTurnCount)
            {
                throw new ArgumentException("Conversation history exceeds its turn limit.", nameof(turns));
            }

            copiedTurns.Add(new ResidentConversationTurn(
                turn.VisitorName,
                turn.EventName,
                turn.Text));
        }

        Revision = revision;
        Summary = summary;
        this.turns = copiedTurns.AsReadOnly();
        CompactCount = CompactPrefixCount(copiedTurns.Count);
    }

    internal long Revision { get; }

    internal string Summary { get; }

    /// <summary>
    /// Ordered turns not yet incorporated into Summary. The older prefix of
    /// CompactCount turns is the candidate for the next summary update.
    /// </summary>
    internal IReadOnlyList<ResidentConversationTurn> Turns => turns;

    internal int CompactCount { get; }

    internal static int CompactPrefixCount(int turnCount) =>
        turnCount >= CompactionStartTurnCount
            ? turnCount - VerbatimTurnCount
            : 0;

    internal string ToJson()
    {
        var json = new JObject
        {
            ["revision"] = Revision,
            ["summary"] = Summary,
            ["turns"] = new JArray(),
            ["compactCount"] = CompactCount
        };

        JArray serializedTurns = (JArray)json["turns"]!;
        foreach (ResidentConversationTurn turn in turns)
        {
            serializedTurns.Add(new JObject
            {
                ["visitorName"] = turn.VisitorName,
                ["eventName"] = turn.EventName,
                ["text"] = turn.Text
            });
        }

        return json.ToString(Formatting.None);
    }

    internal static ResidentConversationSnapshot FromJson(string json)
    {
        if (string.IsNullOrEmpty(json) || json.Length > MaximumJsonCharacters)
        {
            throw new FormatException("Conversation snapshot size is invalid.");
        }

        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new FormatException("Conversation snapshot JSON is invalid.", exception);
        }

        if (!HasExactFields(root, SnapshotFields)
            || !TryInteger(root["revision"], out long revision)
            || revision < 0
            || !TryString(root["summary"], out string summary)
            || !ResidentConversationText.IsValidSummary(summary)
            || root["turns"] is not JArray serializedTurns
            || serializedTurns.Count > MaximumTurnCount
            || !TryInteger(root["compactCount"], out long compactCount)
            || compactCount < 0
            || compactCount > int.MaxValue)
        {
            throw new FormatException("Conversation snapshot fields are invalid.");
        }

        var parsedTurns = new List<ResidentConversationTurn>(serializedTurns.Count);
        foreach (JToken token in serializedTurns)
        {
            if (token is not JObject turnObject
                || !HasExactFields(turnObject, TurnFields)
                || !TryString(turnObject["visitorName"], out string visitorName)
                || !TryString(turnObject["eventName"], out string eventName)
                || !TryString(turnObject["text"], out string text)
                || !ResidentConversationTurn.IsValid(visitorName, eventName, text))
            {
                throw new FormatException("Conversation snapshot turn is invalid.");
            }

            parsedTurns.Add(new ResidentConversationTurn(visitorName, eventName, text));
        }

        var snapshot = new ResidentConversationSnapshot(revision, summary, parsedTurns);
        if (snapshot.CompactCount != (int)compactCount)
        {
            throw new FormatException("Conversation snapshot compact count is invalid.");
        }

        return snapshot;
    }

    private static bool HasExactFields(JObject value, IReadOnlyList<string> expectedFields)
    {
        int count = 0;
        foreach (JProperty property in value.Properties())
        {
            count++;
            bool found = false;
            for (int index = 0; index < expectedFields.Count; index++)
            {
                if (string.Equals(property.Name, expectedFields[index], StringComparison.Ordinal))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return count == expectedFields.Count;
    }

    private static bool TryInteger(JToken? token, out long value)
    {
        value = 0;
        return token != null
            && token.Type == JTokenType.Integer
            && token.ToObject<long?>(JsonSerializer.CreateDefault()) is long parsed
            && (value = parsed) >= 0;
    }

    private static bool TryString(JToken? token, out string value)
    {
        value = string.Empty;
        if (token == null || token.Type != JTokenType.String)
        {
            return false;
        }

        value = token.Value<string>() ?? string.Empty;
        return true;
    }
}

internal sealed class ResidentConversationTurn
{
    internal ResidentConversationTurn(string visitorName, string eventName, string text)
    {
        if (!IsValid(visitorName, eventName, text))
        {
            throw new ArgumentException("Conversation turn is invalid.");
        }

        VisitorName = visitorName;
        EventName = eventName;
        Text = text;
    }

    internal string VisitorName { get; }

    /// <summary>Trigger and scene context supplied by the server integration.</summary>
    internal string EventName { get; }

    internal string Text { get; }

    internal static bool IsValid(string visitorName, string eventName, string text) =>
        ResidentConversationText.IsValidField(
            visitorName,
            ResidentConversationSnapshot.MaximumVisitorNameCharacters,
            allowNewlines: false,
            allowEmpty: false)
        && ResidentConversationText.IsValidField(
            eventName,
            ResidentConversationSnapshot.MaximumEventNameCharacters,
            allowNewlines: false,
            allowEmpty: false)
        && ResidentConversationText.IsValidField(
            text,
            ResidentConversationSnapshot.MaximumSpokenTextCharacters,
            allowNewlines: false,
            allowEmpty: false);
}

internal static class ResidentConversationText
{
    internal static bool IsValidSummary(string summary) =>
        summary != null
        && summary.Length <= ResidentConversationSnapshot.MaximumSummaryCharacters
        && !ContainsUnsupportedControl(summary, allowNewlines: true);

    internal static bool IsValidField(
        string value,
        int maximumCharacters,
        bool allowNewlines,
        bool allowEmpty) =>
        value != null
        && (allowEmpty || !string.IsNullOrWhiteSpace(value))
        && value.Length <= maximumCharacters
        && !ContainsUnsupportedControl(value, allowNewlines);

    private static bool ContainsUnsupportedControl(string value, bool allowNewlines)
    {
        foreach (char character in value)
        {
            if (char.IsControl(character)
                && character != '\t'
                && !(allowNewlines && (character == '\r' || character == '\n')))
            {
                return true;
            }
        }

        return false;
    }
}
