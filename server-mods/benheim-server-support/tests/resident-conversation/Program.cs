using Benheim.Resident;
using BenheimServerSupport;

string statePath = Path.Combine(
    Path.GetTempPath(),
    "benheim-resident-conversation-" + Guid.NewGuid().ToString("N"),
    "conversation-state.json");

try
{
    var history = new ResidentConversationHistory(statePath);
    ResidentConversationSnapshot empty = history.Snapshot();
    Expect(empty.Revision == 0 && empty.Summary.Length == 0, "history starts empty");
    Expect(empty.Turns.Count == 0 && empty.CompactCount == 0, "empty history has no compaction prefix");

    string[] operationIds = Enumerable.Range(0, 12)
        .Select(_ => Guid.NewGuid().ToString("N"))
        .ToArray();
    for (int index = 0; index < operationIds.Length - 1; index++)
    {
        ResidentConversationRecordResult result = history.RecordUtterance(
            operationIds[index],
            "Traveler " + (index + 1),
            index % 2 == 0 ? "talk; evening; meadows" : "approach; rain; black forest",
            "Spoken line " + (index + 1));
        Expect(result == ResidentConversationRecordResult.Recorded,
            "accepted speech appends exactly once");
    }

    ResidentConversationSnapshot belowThreshold = history.Snapshot();
    Expect(belowThreshold.Turns.Count == 11 && belowThreshold.CompactCount == 0,
        "compaction stays off below twelve turns");
    Expect(ResidentConversationSnapshot.FromJson(belowThreshold.ToJson()).CompactCount == 0,
        "snapshot parsing preserves the below-threshold boundary");

    Expect(history.RecordUtterance(
            operationIds[11],
            "Traveler 12",
            "talk; evening; meadows",
            "Spoken line 12") == ResidentConversationRecordResult.Recorded,
        "the twelfth utterance crosses the compaction threshold");

    ResidentConversationSnapshot first = history.Snapshot();
    Expect(first.Revision == 12, "each accepted utterance advances the revision");
    Expect(first.Turns.Count == 12 && first.CompactCount == 6,
        "at twelve turns the older six form the prefix and six recent turns stay verbatim");

    string contextJson = first.ToJson();
    Expect(!contextJson.Contains("operationIds", StringComparison.Ordinal)
        && !contextJson.Contains(operationIds[0], StringComparison.Ordinal),
        "client context does not expose operation IDs");
    ResidentConversationSnapshot roundTrip = ResidentConversationSnapshot.FromJson(contextJson);
    Expect(roundTrip.Revision == first.Revision
        && roundTrip.Summary == first.Summary
        && roundTrip.Turns.Count == first.Turns.Count
        && roundTrip.Turns[0].VisitorName == first.Turns[0].VisitorName
        && roundTrip.Turns[0].EventName == first.Turns[0].EventName
        && roundTrip.Turns[0].Text == first.Turns[0].Text
        && roundTrip.CompactCount == first.CompactCount,
        "snapshot JSON round-trips ordered visitor, event, and speech fields");

    var maximumSnapshot = new ResidentConversationSnapshot(
        first.Revision,
        new string('"', ResidentConversationSnapshot.MaximumSummaryCharacters),
        Enumerable.Range(0, ResidentConversationSnapshot.MaximumTurnCount)
            .Select(index => new ResidentConversationTurn(
                new string('"', ResidentConversationSnapshot.MaximumVisitorNameCharacters),
                new string('"', ResidentConversationSnapshot.MaximumEventNameCharacters),
                new string('"', ResidentConversationSnapshot.MaximumSpokenTextCharacters))));
    Expect(maximumSnapshot.ToJson().Length <= ResidentConversationSnapshot.MaximumJsonCharacters,
        "valid maximum-size client context fits the 64 KiB JSON bound");
    ExpectThrows<FormatException>(
        () => ResidentConversationSnapshot.FromJson(
            new string(' ', ResidentConversationSnapshot.MaximumJsonCharacters + 1)),
        "oversized client context is rejected before parsing");

    Expect(history.RecordUtterance(
            operationIds[0].ToUpperInvariant(),
            "Different visitor",
            "talk",
            "duplicate line") == ResidentConversationRecordResult.DuplicateOperation,
        "operation IDs are deduplicated independent of hex case");

    Expect(history.RecordUtterance(
            Guid.NewGuid().ToString("N"),
            "Traveler 13",
            "talk",
            "New line") == ResidentConversationRecordResult.Recorded,
        "a concurrent newer turn is accepted");
    Expect(!history.TryCompact(first.Revision, first.CompactCount, "stale continuity"),
        "a summary for a stale revision is rejected");

    ResidentConversationSnapshot afterStaleSummary = history.Snapshot();
    Expect(afterStaleSummary.Summary.Length == 0
        && afterStaleSummary.Turns.Count == 13
        && afterStaleSummary.CompactCount == 7,
        "stale compaction leaves summary and original turns intact");

    Expect(!history.TryCompact(
            afterStaleSummary.Revision,
            afterStaleSummary.CompactCount - 1,
            "wrong prefix")
        && !history.TryCompact(
            afterStaleSummary.Revision,
            afterStaleSummary.CompactCount,
            string.Empty)
        && !history.TryCompact(
            afterStaleSummary.Revision,
            afterStaleSummary.CompactCount,
            new string('x', ResidentConversationSnapshot.MaximumSummaryCharacters + 1)),
        "invalid prefix lengths and overlong summaries are rejected");

    Expect(history.TryCompact(
            afterStaleSummary.Revision,
            afterStaleSummary.CompactCount,
            "The visitors have been discussing the resident's favorite evenings."),
        "a current compaction replaces the summary and drops only its covered prefix");
    ResidentConversationSnapshot compacted = history.Snapshot();
    Expect(compacted.Revision == afterStaleSummary.Revision + 1
        && compacted.Summary.Length > 0
        && compacted.Turns.Count == ResidentConversationSnapshot.VerbatimTurnCount
        && compacted.CompactCount == 0,
        "compaction retains the summary and latest six turns");

    var reloaded = new ResidentConversationHistory(statePath);
    ResidentConversationSnapshot restored = reloaded.Snapshot();
    Expect(restored.Revision == compacted.Revision
        && restored.Summary == compacted.Summary
        && restored.Turns.Count == compacted.Turns.Count
        && restored.Turns[0].Text == compacted.Turns[0].Text,
        "atomic state survives history reconstruction");
    Expect(reloaded.RecordUtterance(
            operationIds[0],
            "Traveler 1",
            "talk",
            "replayed after compaction") == ResidentConversationRecordResult.DuplicateOperation,
        "deduplication survives restart and compaction");

    ResidentConversationSnapshot beforeInvalid = reloaded.Snapshot();
    Expect(reloaded.RecordUtterance("not-an-operation-id", "Traveler", "talk", "line")
            == ResidentConversationRecordResult.InvalidOperationId,
        "operation identifiers are validated");
    Expect(reloaded.RecordUtterance(
            Guid.NewGuid().ToString("N"),
            "Traveler",
            "talk",
            new string('x', ResidentConversationSnapshot.MaximumSpokenTextCharacters + 1))
            == ResidentConversationRecordResult.InvalidText,
        "spoken text is bounded");
    Expect(reloaded.Snapshot().Revision == beforeInvalid.Revision,
        "invalid records do not mutate history");

    ExpectThrows<FormatException>(
        () => ResidentConversationSnapshot.FromJson(contextJson.Replace(
            "\"compactCount\":6",
            "\"compactCount\":99",
            StringComparison.Ordinal)),
        "snapshot parsing rejects inconsistent compact counts");

    string boundedStatePath = Path.Combine(
        Path.GetDirectoryName(statePath)!,
        "bounded-state.json");
    var boundedHistory = new ResidentConversationHistory(boundedStatePath);
    for (int index = 0; index < ResidentConversationSnapshot.CompactionStartTurnCount; index++)
    {
        Expect(boundedHistory.RecordUtterance(
                Guid.NewGuid().ToString("N"),
                new string('"', ResidentConversationSnapshot.MaximumVisitorNameCharacters),
                new string('"', ResidentConversationSnapshot.MaximumEventNameCharacters),
                new string('"', ResidentConversationSnapshot.MaximumSpokenTextCharacters))
            == ResidentConversationRecordResult.Recorded,
            "bounded state accepts each turn up to its summary boundary");
    }

    ResidentConversationSnapshot boundedSnapshot = boundedHistory.Snapshot();
    Expect(boundedSnapshot.CompactCount == 6
        && boundedHistory.TryCompact(
            boundedSnapshot.Revision,
            boundedSnapshot.CompactCount,
            new string('"', ResidentConversationSnapshot.MaximumSummaryCharacters)),
        "bounded state can persist a maximum-size summary after compaction");
    for (int index = ResidentConversationSnapshot.VerbatimTurnCount;
         index < ResidentConversationSnapshot.MaximumTurnCount;
         index++)
    {
        Expect(boundedHistory.RecordUtterance(
                Guid.NewGuid().ToString("N"),
                new string('"', ResidentConversationSnapshot.MaximumVisitorNameCharacters),
                new string('"', ResidentConversationSnapshot.MaximumEventNameCharacters),
                new string('"', ResidentConversationSnapshot.MaximumSpokenTextCharacters))
            == ResidentConversationRecordResult.Recorded,
            "bounded state remains writable through its maximum turn count");
    }

    Expect(new FileInfo(boundedStatePath).Length <= ResidentConversationSnapshot.MaximumJsonCharacters,
        "persisted maximum state fits the shared 64 KiB JSON limit");
    ResidentConversationSnapshot atCapacity = boundedHistory.Snapshot();
    Expect(boundedHistory.RecordUtterance(
            Guid.NewGuid().ToString("N"),
            "Traveler",
            "talk",
            "one too many") == ResidentConversationRecordResult.CapacityExceeded
        && boundedHistory.Snapshot().Revision == atCapacity.Revision,
        "the turn cap refuses growth without mutating persisted state");

    Console.WriteLine("Resident conversation history tests passed.");
}
finally
{
    string? directory = Path.GetDirectoryName(statePath);
    if (directory != null && Directory.Exists(directory))
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void ExpectThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
