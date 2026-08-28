using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BenheimQoL.Infrastructure;

// Catalog records reuse the typed diagnostic serializer, but the command
// writes them directly to its local snapshot. Diagnostics.Emit is never called,
// so catalog entries cannot enter optional remote diagnostics.
internal sealed class RuntimePrimitiveRecord
{
    private const string CatalogSession = "manual-runtime-catalog";
    private readonly DiagnosticEvent diagnosticEvent;
    private readonly List<string> searchableValues = new List<string>();

    internal RuntimePrimitiveRecord(string category, string donorKind, string identity)
    {
        Category = category;
        DonorKind = donorKind;
        Identity = identity;
        diagnosticEvent = DiagnosticEvent.Create(category, donorKind)
            .String("donor_kind", donorKind)
            .String("identity", identity);
        searchableValues.Add(category);
        searchableValues.Add(donorKind);
        searchableValues.Add(identity);
    }

    internal string Category { get; }
    internal string DonorKind { get; }
    internal string Identity { get; }

    internal RuntimePrimitiveRecord String(string name, string? value)
    {
        diagnosticEvent.String(name, value);
        searchableValues.Add(name);
        searchableValues.Add(value ?? "null");
        return this;
    }

    internal RuntimePrimitiveRecord Integer(string name, int value)
    {
        diagnosticEvent.Integer(name, value);
        searchableValues.Add(name);
        searchableValues.Add(value.ToString(CultureInfo.InvariantCulture));
        return this;
    }

    internal RuntimePrimitiveRecord Boolean(string name, bool value)
    {
        diagnosticEvent.Boolean(name, value);
        searchableValues.Add(name);
        searchableValues.Add(value ? "true" : "false");
        return this;
    }

    internal bool Matches(string filter)
    {
        if (filter.Length == 0)
        {
            return true;
        }

        foreach (string value in searchableValues)
        {
            if (value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    internal string ToConsoleLine()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(DonorKind).Append(' ').Append(Flatten(Identity));
        for (int index = 3; index + 1 < searchableValues.Count; index += 2)
        {
            builder
                .Append(' ')
                .Append(searchableValues[index])
                .Append('=')
                .Append(Flatten(searchableValues[index + 1]));
        }
        return builder.ToString();
    }

    internal string ToJsonLine(DateTime createdUtc, string benheimVersion)
    {
        diagnosticEvent.Prepare(createdUtc, CatalogSession, benheimVersion);
        return diagnosticEvent.ToJsonLine();
    }

    internal static int CompareStableIdentity(
        RuntimePrimitiveRecord left,
        RuntimePrimitiveRecord right)
    {
        int identityComparison = string.Compare(left.Identity, right.Identity, StringComparison.Ordinal);
        return identityComparison != 0
            ? identityComparison
            : string.Compare(left.DonorKind, right.DonorKind, StringComparison.Ordinal);
    }

    private static string Flatten(string value)
    {
        return value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace(' ', '_');
    }
}
