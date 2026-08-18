using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BenheimInventoryProtocol;

internal enum InventoryTransactionDiagnosticLevel
{
    Info,
    Warning
}

internal enum InventoryTransactionDiagnosticValueKind
{
    String,
    Integer,
    Number,
    Boolean
}

internal readonly struct InventoryTransactionDiagnosticField
{
    private InventoryTransactionDiagnosticField(
        string name,
        InventoryTransactionDiagnosticValueKind kind,
        string? text,
        long integer,
        double number,
        bool boolean)
    {
        Name = name;
        Kind = kind;
        Text = text;
        Integer = integer;
        Number = number;
        Boolean = boolean;
    }

    internal string Name { get; }
    internal InventoryTransactionDiagnosticValueKind Kind { get; }
    internal string? Text { get; }
    internal long Integer { get; }
    internal double Number { get; }
    internal bool Boolean { get; }

    internal static InventoryTransactionDiagnosticField Code(string name, string value) =>
        new InventoryTransactionDiagnosticField(
            name,
            InventoryTransactionDiagnosticValueKind.String,
            value,
            0L,
            0d,
            false);

    internal static InventoryTransactionDiagnosticField Count(string name, long value) =>
        new InventoryTransactionDiagnosticField(
            name,
            InventoryTransactionDiagnosticValueKind.Integer,
            null,
            value,
            0d,
            false);

    internal static InventoryTransactionDiagnosticField Duration(string name, double value) =>
        new InventoryTransactionDiagnosticField(
            name,
            InventoryTransactionDiagnosticValueKind.Number,
            null,
            0L,
            value,
            false);

    internal static InventoryTransactionDiagnosticField Flag(string name, bool value) =>
        new InventoryTransactionDiagnosticField(
            name,
            InventoryTransactionDiagnosticValueKind.Boolean,
            null,
            0L,
            0d,
            value);
}

/// <summary>
/// Typed telemetry shared by the requester, server router, and chest-owner
/// protocol roles. Deliberate construction sites own every emitted field. Host
/// adapters decide where the complete event goes; this shared model does not
/// filter field names or value shapes.
/// </summary>
internal sealed class InventoryTransactionDiagnosticEvent
{
    internal const string Domain = "InventoryTransaction";

    private readonly List<InventoryTransactionDiagnosticField> fields =
        new List<InventoryTransactionDiagnosticField>();
    private readonly HashSet<string> fieldNames = new HashSet<string>(StringComparer.Ordinal);

    private InventoryTransactionDiagnosticEvent(
        string name,
        string peerRole,
        InventoryTransactionDiagnosticLevel level)
    {
        RequireString("event", name);
        RequireString("peer_role", peerRole);
        Name = name;
        Level = level;
        fieldNames.Add("peer_role");
        fields.Add(InventoryTransactionDiagnosticField.Code("peer_role", peerRole));
    }

    internal string Name { get; }
    internal InventoryTransactionDiagnosticLevel Level { get; }
    internal IReadOnlyList<InventoryTransactionDiagnosticField> Fields => fields;

    internal static InventoryTransactionDiagnosticEvent Create(
        string name,
        string peerRole,
        InventoryTransactionDiagnosticLevel level = InventoryTransactionDiagnosticLevel.Info) =>
        new InventoryTransactionDiagnosticEvent(name, peerRole, level);

    internal InventoryTransactionDiagnosticEvent Code(string name, string value)
    {
        RequireNewName(name);
        RequireString(name, value);
        fields.Add(InventoryTransactionDiagnosticField.Code(name, value));
        return this;
    }

    internal InventoryTransactionDiagnosticEvent Integer(string name, long value)
    {
        RequireNewName(name);
        fields.Add(InventoryTransactionDiagnosticField.Count(name, value));
        return this;
    }

    internal InventoryTransactionDiagnosticEvent Text(string name, string value)
    {
        return Code(name, value);
    }

    internal InventoryTransactionDiagnosticEvent Number(string name, double value)
    {
        RequireNewName(name);
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException($"Unsupported diagnostic number field: {name}");
        }

        fields.Add(InventoryTransactionDiagnosticField.Duration(name, value));
        return this;
    }

    internal InventoryTransactionDiagnosticEvent Boolean(string name, bool value)
    {
        RequireNewName(name);
        fields.Add(InventoryTransactionDiagnosticField.Flag(name, value));
        return this;
    }

    internal string ToReadableLine()
    {
        StringBuilder line = new StringBuilder();
        line.Append("[diag][").Append(Domain).Append("] ").Append(Name);
        foreach (InventoryTransactionDiagnosticField field in fields)
        {
            line.Append(' ').Append(field.Name).Append('=');
            switch (field.Kind)
            {
                case InventoryTransactionDiagnosticValueKind.String:
                    line.Append(field.Text);
                    break;
                case InventoryTransactionDiagnosticValueKind.Integer:
                    line.Append(field.Integer.ToString(CultureInfo.InvariantCulture));
                    break;
                case InventoryTransactionDiagnosticValueKind.Number:
                    line.Append(field.Number.ToString("0.###", CultureInfo.InvariantCulture));
                    break;
                case InventoryTransactionDiagnosticValueKind.Boolean:
                    line.Append(field.Boolean ? "true" : "false");
                    break;
            }
        }

        return line.ToString();
    }

    private void RequireNewName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 128 || !fieldNames.Add(name))
        {
            throw new InvalidOperationException($"Invalid or duplicate diagnostic field name: {name}");
        }
    }

    private static void RequireString(string name, string value)
    {
        if (value == null || value.Length > 32768)
        {
            throw new InvalidOperationException($"Invalid diagnostic string field: {name}");
        }
    }
}

internal interface IInventoryTransactionDiagnosticSink
{
    void Emit(InventoryTransactionDiagnosticEvent diagnosticEvent);
}

/// <summary>
/// Keeps observability outside the transaction's liveness boundary. A broken
/// logger, serializer, or remote diagnostic sink must never interrupt item
/// settlement, result delivery, or cleanup.
/// </summary>
internal static class InventoryTransactionDiagnosticProjection
{
    internal static void EmitBestEffort(
        IInventoryTransactionDiagnosticSink? sink,
        InventoryTransactionDiagnosticEvent diagnosticEvent)
    {
        if (sink == null)
        {
            return;
        }

        try
        {
            sink.Emit(diagnosticEvent);
        }
        catch (Exception)
        {
            // Diagnostics have no authority over transaction progress. There
            // is intentionally no fallback emission here: the fallback could
            // fail for the same reason and re-enter the transaction path.
        }
    }
}
