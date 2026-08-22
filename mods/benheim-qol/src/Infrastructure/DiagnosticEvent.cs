using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BenheimQoL.Infrastructure;

// One immutable-after-emission value owns both diagnostic representations.
// Historical free-form diagnostics remain text-only until their domain needs
// stable fields, which avoids laundering arbitrary suffixes into JSON.
internal sealed class DiagnosticEvent
{
    internal const int CurrentSchema = 1;
    internal const int RemoteSchema = 2;
    private static readonly HashSet<string> ReservedFieldNames = new HashSet<string>(StringComparer.Ordinal)
    {
        "_time",
        "session_id",
        "client_id",
        "player_name",
        "peer_id",
        "mod_version",
        "build_id",
        "domain",
        "event",
        "schema",
        "fields"
    };

    private readonly List<DiagnosticField> fields = new List<DiagnosticField>();
    private readonly HashSet<string> fieldNames = new HashSet<string>(StringComparer.Ordinal);
    private DateTime timestampUtc;
    private string session = string.Empty;
    private string benheimVersion = string.Empty;
    private bool prepared;

    private DiagnosticEvent(string domain, string name)
    {
        Domain = domain;
        Name = name;
    }

    internal string Domain { get; }
    internal string Name { get; }

    internal static DiagnosticEvent Create(string domain, string name)
    {
        return new DiagnosticEvent(domain, name);
    }

    internal DiagnosticEvent String(string name, string? value)
    {
        return AddField(DiagnosticField.String(name, value));
    }

    internal DiagnosticEvent Integer(string name, int value)
    {
        return AddField(DiagnosticField.Integer(name, value));
    }

    internal DiagnosticEvent Integer(string name, long value)
    {
        return AddField(DiagnosticField.Integer(name, value));
    }

    internal DiagnosticEvent Number(string name, float value)
    {
        return AddField(DiagnosticField.Number(name, value));
    }

    internal DiagnosticEvent Number(string name, double value)
    {
        return AddField(DiagnosticField.Number(name, value));
    }

    internal DiagnosticEvent Boolean(string name, bool value)
    {
        return AddField(DiagnosticField.Boolean(name, value));
    }

    private DiagnosticEvent AddField(DiagnosticField field)
    {
        EnsureDefinitionOpen();
        if (ReservedFieldNames.Contains(field.Name))
        {
            throw new InvalidOperationException($"Diagnostic field name '{field.Name}' is reserved for the event envelope.");
        }
        if (!fieldNames.Add(field.Name))
        {
            throw new InvalidOperationException($"Diagnostic field names must be unique; '{field.Name}' is duplicated.");
        }
        fields.Add(field);
        return this;
    }

    internal void Prepare(DateTime utcNow, string sessionId, string version)
    {
        if (prepared)
        {
            throw new InvalidOperationException("A diagnostic event can be emitted only once.");
        }

        timestampUtc = utcNow.ToUniversalTime();
        session = sessionId;
        benheimVersion = version;
        prepared = true;
    }

    private void EnsureDefinitionOpen()
    {
        if (prepared)
        {
            throw new InvalidOperationException("A diagnostic event cannot change after emission.");
        }
    }

    internal string ToReadableLine()
    {
        StringBuilder builder = new StringBuilder();
        builder.Append("[diag][").Append(Domain).Append("] ").Append(Name);
        foreach (DiagnosticField field in fields)
        {
            builder.Append(' ').Append(field.Name).Append('=').Append(field.ReadableValue());
        }
        return builder.ToString();
    }

    internal string ToJsonLine()
    {
        if (!prepared)
        {
            throw new InvalidOperationException("Prepare the diagnostic event before serialization.");
        }

        StringBuilder builder = new StringBuilder(256);
        builder.Append('{');
        AppendJsonStringProperty(builder, "timestamp", timestampUtc.ToString("O", CultureInfo.InvariantCulture));
        builder.Append(',');
        AppendJsonStringProperty(builder, "session", session);
        builder.Append(',');
        AppendJsonStringProperty(builder, "benheim_version", benheimVersion);
        builder.Append(',');
        AppendJsonStringProperty(builder, "domain", Domain);
        builder.Append(',');
        AppendJsonStringProperty(builder, "event", Name);
        builder.Append(",\"schema\":").Append(CurrentSchema.ToString(CultureInfo.InvariantCulture));
        foreach (DiagnosticField field in fields)
        {
            builder.Append(',');
            AppendJsonString(builder, field.Name);
            builder.Append(':').Append(field.JsonValue());
        }
        builder.Append('}');
        return builder.ToString();
    }

    // Private test builds forward the typed gameplay evidence that owns the
    // local line. Keep common selectors in a stable Axiom envelope and place
    // every producer-owned field in the configured map. This prevents new
    // gameplay attributes from expanding the dataset schema.
    internal string ToRemoteJsonLine(
        string clientId,
        string playerName,
        string peerId,
        string buildId)
    {
        if (!prepared)
        {
            throw new InvalidOperationException("Prepare the diagnostic event before serialization.");
        }

        string timestamp = timestampUtc.ToString("O", CultureInfo.InvariantCulture);
        StringBuilder builder = new StringBuilder(256);
        builder.Append('{');
        AppendJsonStringProperty(builder, "_time", timestamp);
        builder.Append(',');
        AppendJsonStringProperty(builder, "session_id", session);
        builder.Append(',');
        AppendJsonStringProperty(builder, "client_id", clientId);
        builder.Append(',');
        AppendJsonStringProperty(builder, "player_name", playerName);
        if (!string.IsNullOrEmpty(peerId))
        {
            builder.Append(',');
            AppendJsonStringProperty(builder, "peer_id", peerId);
        }
        builder.Append(',');
        AppendJsonStringProperty(builder, "mod_version", benheimVersion);
        builder.Append(',');
        AppendJsonStringProperty(builder, "build_id", buildId);
        builder.Append(',');
        AppendJsonStringProperty(builder, "domain", Domain);
        builder.Append(',');
        AppendJsonStringProperty(builder, "event", Name);
        builder.Append(",\"schema\":").Append(RemoteSchema.ToString(CultureInfo.InvariantCulture));
        foreach (DiagnosticField field in fields)
        {
            if (field.Name != "operation_id")
            {
                continue;
            }

            builder.Append(',');
            AppendJsonString(builder, field.Name);
            builder.Append(':').Append(field.JsonValue());
            break;
        }

        builder.Append(",\"fields\":{");
        int appendedFields = 0;
        foreach (DiagnosticField field in fields)
        {
            if (appendedFields > 0)
            {
                builder.Append(',');
            }
            AppendJsonString(builder, field.Name);
            builder.Append(':').Append(field.JsonValue());
            appendedFields++;
        }
        builder.Append("}}");
        return builder.ToString();
    }

    private static void AppendJsonStringProperty(StringBuilder builder, string name, string value)
    {
        AppendJsonString(builder, name);
        builder.Append(':');
        AppendJsonString(builder, value);
    }

    private static void AppendJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    private enum DiagnosticFieldKind
    {
        String,
        Integer,
        Number,
        Boolean
    }

    private readonly struct DiagnosticField
    {
        private DiagnosticField(string name, DiagnosticFieldKind kind, string? text, long integer, double number, bool boolean)
        {
            Name = name;
            Kind = kind;
            Text = text;
            IntegerValue = integer;
            NumberValue = number;
            BooleanValue = boolean;
        }

        internal string Name { get; }
        private DiagnosticFieldKind Kind { get; }
        private string? Text { get; }
        private long IntegerValue { get; }
        private double NumberValue { get; }
        private bool BooleanValue { get; }

        internal static DiagnosticField String(string name, string? value) =>
            new DiagnosticField(name, DiagnosticFieldKind.String, value, 0, 0, false);
        internal static DiagnosticField Integer(string name, long value) =>
            new DiagnosticField(name, DiagnosticFieldKind.Integer, null, value, 0, false);
        internal static DiagnosticField Number(string name, double value) =>
            new DiagnosticField(name, DiagnosticFieldKind.Number, null, 0, value, false);
        internal static DiagnosticField Boolean(string name, bool value) =>
            new DiagnosticField(name, DiagnosticFieldKind.Boolean, null, 0, 0, value);

        internal string ReadableValue()
        {
            return Kind switch
            {
                DiagnosticFieldKind.String => Text == null ? "null" : Flatten(Text),
                DiagnosticFieldKind.Integer => IntegerValue.ToString(CultureInfo.InvariantCulture),
                DiagnosticFieldKind.Number => NumberValue.ToString("0.###", CultureInfo.InvariantCulture),
                DiagnosticFieldKind.Boolean => BooleanValue ? "true" : "false",
                _ => throw new InvalidOperationException("Unknown diagnostic field kind.")
            };
        }

        internal string JsonValue()
        {
            switch (Kind)
            {
                case DiagnosticFieldKind.String:
                    if (Text == null)
                    {
                        return "null";
                    }
                    StringBuilder builder = new StringBuilder();
                    AppendJsonString(builder, Text);
                    return builder.ToString();
                case DiagnosticFieldKind.Integer:
                    return IntegerValue.ToString(CultureInfo.InvariantCulture);
                case DiagnosticFieldKind.Number:
                    return double.IsNaN(NumberValue) || double.IsInfinity(NumberValue)
                        ? "null"
                        : NumberValue.ToString("R", CultureInfo.InvariantCulture);
                case DiagnosticFieldKind.Boolean:
                    return BooleanValue ? "true" : "false";
                default:
                    throw new InvalidOperationException("Unknown diagnostic field kind.");
            }
        }

        private static string Flatten(string value)
        {
            return value
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Replace(' ', '_');
        }
    }
}
