using System;
using System.Collections.Generic;

namespace BenheimQoL.Infrastructure;

internal sealed class RuntimePrimitiveCatalogSelection
{
    private RuntimePrimitiveCatalogSelection(
        int sourceCount,
        List<RuntimePrimitiveRecord> matches,
        int writtenCount)
    {
        SourceCount = sourceCount;
        Matches = matches;
        WrittenCount = writtenCount;
    }

    internal int SourceCount { get; }
    internal List<RuntimePrimitiveRecord> Matches { get; }
    internal int WrittenCount { get; }

    internal static RuntimePrimitiveCatalogSelection Create(
        List<RuntimePrimitiveRecord> source,
        string filter,
        int maximumEntries)
    {
        if (maximumEntries < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        }

        List<RuntimePrimitiveRecord> matches =
            source.FindAll(record => record.Matches(filter));
        return new RuntimePrimitiveCatalogSelection(
            source.Count,
            matches,
            Math.Min(matches.Count, maximumEntries));
    }

    internal RuntimePrimitiveRecord CreateSummary(
        string category,
        string filter,
        DateTime createdUtc)
    {
        return new RuntimePrimitiveRecord(category, "summary", $"summary:{category}")
            .String("created_utc", createdUtc.ToUniversalTime().ToString("O"))
            .String("filter", filter.Length == 0 ? null : filter)
            .Integer("source_count", SourceCount)
            .Integer("matched_count", Matches.Count)
            .Integer("written_count", WrittenCount)
            .Boolean("truncated", WrittenCount < Matches.Count);
    }
}
