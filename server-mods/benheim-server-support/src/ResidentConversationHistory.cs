using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Benheim.Resident;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BenheimServerSupport;

internal enum ResidentConversationRecordResult
{
    Recorded,
    DuplicateOperation,
    InvalidOperationId,
    InvalidVisitorName,
    InvalidEventName,
    InvalidText,
    CapacityExceeded
}

/// <summary>
/// Server-owned conversation state shared by every compatible visitor. The
/// private state file is separate from the append-only speech trace. Summary
/// replacement and turn appends advance one revision so a provider response
/// can only compact the exact history it observed.
/// </summary>
internal sealed class ResidentConversationHistory
{
    private const int StateSchemaVersion = 1;
    private const int MaximumRememberedOperations = 24;

    private static readonly string[] StateFields =
    {
        "schemaVersion", "revision", "summary", "turns", "operationIds"
    };

    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private readonly object sync = new();
    private readonly ResidentConversationStateStore store;
    private State state;

    internal ResidentConversationHistory(string stateFilePath)
    {
        if (string.IsNullOrWhiteSpace(stateFilePath))
        {
            throw new ArgumentException("A private conversation state path is required.", nameof(stateFilePath));
        }

        store = new ResidentConversationStateStore(Path.GetFullPath(stateFilePath));
        state = store.Load();
    }

    internal ResidentConversationSnapshot Snapshot()
    {
        lock (sync)
        {
            return new ResidentConversationSnapshot(
                state.Revision,
                state.Summary,
                state.Turns);
        }
    }

    internal ResidentConversationRecordResult RecordUtterance(
        string operationId,
        string visitorName,
        string eventName,
        string text)
    {
        if (!IsOperationId(operationId, out string normalizedOperationId))
        {
            return ResidentConversationRecordResult.InvalidOperationId;
        }

        lock (sync)
        {
            if (state.OperationIds.Contains(normalizedOperationId, StringComparer.Ordinal))
            {
                return ResidentConversationRecordResult.DuplicateOperation;
            }

            if (!ResidentConversationText.IsValidField(
                    visitorName,
                    ResidentConversationSnapshot.MaximumVisitorNameCharacters,
                    allowNewlines: false,
                    allowEmpty: false))
            {
                return ResidentConversationRecordResult.InvalidVisitorName;
            }

            if (!ResidentConversationText.IsValidField(
                    eventName,
                    ResidentConversationSnapshot.MaximumEventNameCharacters,
                    allowNewlines: false,
                    allowEmpty: false))
            {
                return ResidentConversationRecordResult.InvalidEventName;
            }

            if (!ResidentConversationText.IsValidField(
                    text,
                    ResidentConversationSnapshot.MaximumSpokenTextCharacters,
                    allowNewlines: false,
                    allowEmpty: false))
            {
                return ResidentConversationRecordResult.InvalidText;
            }

            if (state.Revision == long.MaxValue
                || state.Turns.Count >= ResidentConversationSnapshot.MaximumTurnCount)
            {
                return ResidentConversationRecordResult.CapacityExceeded;
            }

            var turns = new List<ResidentConversationTurn>(state.Turns)
            {
                new ResidentConversationTurn(visitorName, eventName, text)
            };
            var operationIds = new List<string>(state.OperationIds)
            {
                normalizedOperationId
            };
            if (operationIds.Count > MaximumRememberedOperations)
            {
                operationIds.RemoveAt(0);
            }

            var next = new State(
                state.Revision + 1,
                state.Summary,
                turns,
                operationIds);
            store.Save(next);
            state = next;
            return ResidentConversationRecordResult.Recorded;
        }
    }

    /// <summary>
    /// Replace the summary and remove only the older prefix named by a current
    /// snapshot. False means the snapshot is stale or the proposed compaction
    /// is invalid; no state is changed in either case.
    /// </summary>
    internal bool TryCompact(long snapshotRevision, int compactCount, string summary)
    {
        if (!ResidentConversationText.IsValidSummary(summary)
            || string.IsNullOrWhiteSpace(summary))
        {
            return false;
        }

        lock (sync)
        {
            int expectedCompactCount = ResidentConversationSnapshot.CompactPrefixCount(
                state.Turns.Count);
            if (snapshotRevision != state.Revision
                || state.Revision == long.MaxValue
                || compactCount <= 0
                || compactCount != expectedCompactCount)
            {
                return false;
            }

            var retainedTurns = state.Turns
                .Skip(compactCount)
                .ToList();
            var next = new State(
                state.Revision + 1,
                summary,
                retainedTurns,
                new List<string>(state.OperationIds));
            store.Save(next);
            state = next;
            return true;
        }
    }

    private static bool IsOperationId(string operationId, out string normalized)
    {
        normalized = string.Empty;
        if (operationId == null
            || operationId.Length != 32
            || !Guid.TryParseExact(operationId, "N", out Guid parsed))
        {
            return false;
        }

        normalized = parsed.ToString("N");
        return true;
    }

    private sealed class State
    {
        internal State(
            long revision,
            string summary,
            List<ResidentConversationTurn> turns,
            List<string> operationIds)
        {
            Revision = revision;
            Summary = summary;
            Turns = turns;
            OperationIds = operationIds;
        }

        internal long Revision { get; }

        internal string Summary { get; }

        internal List<ResidentConversationTurn> Turns { get; }

        internal List<string> OperationIds { get; }

        internal static State Empty() => new(
            0,
            string.Empty,
            new List<ResidentConversationTurn>(),
            new List<string>());
    }

    /// <summary>
    /// Stores only bounded recent turns, the carried summary, and a bounded
    /// idempotency window. A same-directory rename keeps the prior state file
    /// intact unless the full replacement was written and flushed.
    /// </summary>
    private sealed class ResidentConversationStateStore
    {
        private static readonly string[] SnapshotFields =
        {
            "visitorName", "eventName", "text"
        };

        private readonly string path;

        internal ResidentConversationStateStore(string path) => this.path = path;

        internal State Load()
        {
            if (!File.Exists(path))
            {
                return State.Empty();
            }

            string json = File.ReadAllText(path, Encoding.UTF8);
            if (json.Length > ResidentConversationSnapshot.MaximumJsonCharacters)
            {
                throw new InvalidDataException("resident_conversation_state_too_large");
            }

            JObject root;
            try
            {
                root = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("resident_conversation_state_invalid_json", exception);
            }

            if (!HasExactFields(root, StateFields)
                || !TryInteger(root["schemaVersion"], out long schemaVersion)
                || schemaVersion != StateSchemaVersion
                || !TryInteger(root["revision"], out long revision)
                || !TryString(root["summary"], out string summary)
                || !ResidentConversationText.IsValidSummary(summary)
                || root["turns"] is not JArray serializedTurns
                || serializedTurns.Count > ResidentConversationSnapshot.MaximumTurnCount
                || root["operationIds"] is not JArray serializedOperationIds
                || serializedOperationIds.Count > MaximumRememberedOperations)
            {
                throw new InvalidDataException("resident_conversation_state_invalid_fields");
            }

            var turns = new List<ResidentConversationTurn>(serializedTurns.Count);
            foreach (JToken token in serializedTurns)
            {
                if (token is not JObject turn
                    || !HasExactFields(turn, SnapshotFields)
                    || !TryString(turn["visitorName"], out string visitorName)
                    || !TryString(turn["eventName"], out string eventName)
                    || !TryString(turn["text"], out string text)
                    || !ResidentConversationTurn.IsValid(visitorName, eventName, text))
                {
                    throw new InvalidDataException("resident_conversation_state_invalid_turn");
                }

                turns.Add(new ResidentConversationTurn(visitorName, eventName, text));
            }

            var operationIds = new List<string>(serializedOperationIds.Count);
            var distinctOperationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken token in serializedOperationIds)
            {
                if (!TryString(token, out string operationId)
                    || !IsOperationId(operationId, out string normalizedOperationId)
                    || !distinctOperationIds.Add(normalizedOperationId))
                {
                    throw new InvalidDataException("resident_conversation_state_invalid_operation_id");
                }

                operationIds.Add(normalizedOperationId);
            }

            return new State(revision, summary, turns, operationIds);
        }

        internal void Save(State state)
        {
            string directory = Path.GetDirectoryName(path)
                ?? throw new IOException("resident_conversation_state_directory_missing");
            Directory.CreateDirectory(directory);

            string temporaryPath = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                string json = Serialize(state);
                if (json.Length > ResidentConversationSnapshot.MaximumJsonCharacters)
                {
                    throw new InvalidDataException("resident_conversation_state_too_large");
                }

                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None))
                {
                    using (var writer = new StreamWriter(
                               stream,
                               Utf8WithoutBom,
                               bufferSize: 4096,
                               leaveOpen: true))
                    {
                        writer.Write(json);
                        writer.Flush();
                    }

                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, destinationBackupFileName: null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static string Serialize(State state)
        {
            var root = new JObject
            {
                ["schemaVersion"] = StateSchemaVersion,
                ["revision"] = state.Revision,
                ["summary"] = state.Summary,
                ["turns"] = new JArray(),
                ["operationIds"] = new JArray(state.OperationIds)
            };

            JArray turns = (JArray)root["turns"]!;
            foreach (ResidentConversationTurn turn in state.Turns)
            {
                turns.Add(new JObject
                {
                    ["visitorName"] = turn.VisitorName,
                    ["eventName"] = turn.EventName,
                    ["text"] = turn.Text
                });
            }

            return root.ToString(Formatting.None);
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
            if (token == null || token.Type != JTokenType.Integer)
            {
                return false;
            }

            try
            {
                value = token.Value<long>();
                return value >= 0;
            }
            catch (Exception)
            {
                return false;
            }
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
}
