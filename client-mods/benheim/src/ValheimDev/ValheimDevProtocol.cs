using System;
using System.Collections.Generic;
using System.Text;

namespace BenheimQoL.ValheimDev;

internal sealed class ValheimDevRequest
{
    internal string Kind { get; set; } = string.Empty;
    internal int Protocol { get; set; }
    internal string Token { get; set; } = string.Empty;
    internal string Generation { get; set; } = string.Empty;
    internal string OperationId { get; set; } = string.Empty;
    internal string Source { get; set; } = string.Empty;
    internal string SourceSha256 { get; set; } = string.Empty;
    internal string AssemblySha256 { get; set; } = string.Empty;
    internal string AssemblyBase64 { get; set; } = string.Empty;
    internal string EntryType { get; set; } = string.Empty;
    internal List<string> EvidenceEvents { get; } = new List<string>();
    internal int EvidenceTimeoutMs { get; set; }
}

internal sealed class ValheimDevBuildIdentity
{
    internal string SessionId { get; set; } = string.Empty;
    internal string Generation { get; set; } = string.Empty;
    internal string Token { get; set; } = string.Empty;
    internal string AuthorizedAt { get; set; } = string.Empty;
    internal string ValheimVersion { get; set; } = string.Empty;
    internal string ValheimSha256 { get; set; } = string.Empty;
    internal string BenheimVersion { get; set; } = string.Empty;
    internal string BenheimSha256 { get; set; } = string.Empty;
    internal List<string> CompilerReferences { get; } = new List<string>();
}

internal static class ValheimDevCleanupState
{
    internal const string NotApplicable = "not_applicable";
    internal const string Cleaned = "cleaned";
    internal const string RestartRequired = "restart_required";
}

internal sealed class ValheimDevResponse
{
    internal int Protocol { get; set; } = ValheimDevProtocol.ProtocolVersion;
    internal bool Ok { get; set; }
    internal string? Error { get; set; }
    internal ValheimDevBuildIdentity Identity { get; set; } = new ValheimDevBuildIdentity();
    internal bool Authorized { get; set; }
    internal string OperationId { get; set; } = string.Empty;
    internal string StartedUtc { get; set; } = string.Empty;
    internal string FinishedUtc { get; set; } = string.Empty;
    internal string? Result { get; set; }
    internal string? Exception { get; set; }
    internal string CleanupState { get; set; } = ValheimDevCleanupState.NotApplicable;
    internal bool EvidenceSelected { get; set; }
    internal bool EvidenceExhaustive { get; set; }
    internal List<string> EvidenceEvents { get; } = new List<string>();

    internal string ToJson(bool apply)
    {
        StringBuilder builder = new StringBuilder(512);
        builder.Append('{');
        ValheimDevJson.AppendProperty(builder, "protocol", Protocol);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "ok", Ok);
        builder.Append(',');
        ValheimDevJson.AppendNullableProperty(builder, "error", Error);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "session_id", Identity.SessionId);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "generation", Identity.Generation);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "valheim_version", Identity.ValheimVersion);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "valheim_sha256", Identity.ValheimSha256);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "benheim_version", Identity.BenheimVersion);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "benheim_sha256", Identity.BenheimSha256);
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "authorized", Authorized);
        if (apply)
        {
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "operation_id", OperationId);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "started_utc", StartedUtc);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "finished_utc", FinishedUtc);
            builder.Append(',');
            ValheimDevJson.AppendNullableProperty(builder, "result", Result);
            builder.Append(',');
            ValheimDevJson.AppendNullableProperty(builder, "exception", Exception);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "cleanup_state", CleanupState);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "evidence_selected", EvidenceSelected);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "evidence_exhaustive", EvidenceExhaustive);
            builder.Append(',');
            ValheimDevJson.AppendStringArrayProperty(builder, "evidence_events", EvidenceEvents);
        }
        builder.Append('}');
        return builder.ToString();
    }
}

internal static class ValheimDevProtocol
{
    internal const int ProtocolVersion = 1;
    internal const int MaximumSourceBytes = 256 * 1024;
    internal const int MaximumAssemblyBytes = 1024 * 1024;
    internal const int MaximumRequestBytes = 2 * 1024 * 1024;
    internal const int MaximumQueueDepth = 8;
    internal const int MaximumEvidenceEvents = 64;
    internal const int MaximumEvidenceBytes = 256 * 1024;
    internal const int MaximumEvidenceTimeoutMs = 120000;
    internal const int MaximumJsonDepth = 16;
    internal const string ExpectedEntryType = "ValheimDevExperiment";

    internal static bool TryParseRequest(string json, out ValheimDevRequest request, out string error)
    {
        request = new ValheimDevRequest();
        error = string.Empty;
        if (Encoding.UTF8.GetByteCount(json) > MaximumRequestBytes)
        {
            error = "request_too_large";
            return false;
        }

        if (!ValheimDevJson.TryParseObject(json, out Dictionary<string, object?> values, out error))
        {
            error = "invalid_json:" + error;
            return false;
        }

        if (!TryString(values, "kind", out string kind)
            || !TryInteger(values, "protocol", out int protocol)
            || !TryString(values, "token", out string token)
            || !TryString(values, "generation", out string generation))
        {
            error = "missing_request_envelope";
            return false;
        }

        if (kind != "status" && kind != "apply")
        {
            error = "unsupported_request_kind";
            return false;
        }

        request.Kind = kind;
        request.Protocol = protocol;
        request.Token = token;
        request.Generation = generation;
        if (kind == "status") return true;

        if (!TryString(values, "operation_id", out string operationId)
            || !TryString(values, "source", out string source)
            || !TryString(values, "source_sha256", out string sourceSha256)
            || !TryString(values, "assembly_sha256", out string assemblySha256)
            || !TryString(values, "assembly", out string assemblyBase64)
            || !TryString(values, "entry_type", out string entryType)
            || !TryInteger(values, "evidence_timeout_ms", out int timeoutMs)
            || !TryStringArray(values, "evidence_events", request.EvidenceEvents))
        {
            error = "missing_apply_fields";
            return false;
        }

        if (string.IsNullOrWhiteSpace(operationId) || operationId.Length > 128)
        {
            error = "invalid_operation_id";
            return false;
        }
        if (Encoding.UTF8.GetByteCount(source) > MaximumSourceBytes)
        {
            error = "source_too_large";
            return false;
        }
        if (assemblyBase64.Length > ((MaximumAssemblyBytes + 2) / 3) * 4 + 8)
        {
            error = "assembly_too_large";
            return false;
        }
        if (timeoutMs < 0 || timeoutMs > MaximumEvidenceTimeoutMs)
        {
            error = "invalid_evidence_timeout";
            return false;
        }
        if (request.EvidenceEvents.Count > MaximumEvidenceEvents)
        {
            error = "too_many_evidence_selectors";
            return false;
        }
        foreach (string selector in request.EvidenceEvents)
        {
            int separator = selector.IndexOf(':');
            if (separator <= 0 || separator == selector.Length - 1 || selector.Length > 128)
            {
                error = "invalid_evidence_selector";
                return false;
            }
        }

        request.OperationId = operationId;
        request.Source = source;
        request.SourceSha256 = sourceSha256;
        request.AssemblySha256 = assemblySha256;
        request.AssemblyBase64 = assemblyBase64;
        request.EntryType = entryType;
        request.EvidenceTimeoutMs = timeoutMs;
        return true;
    }

    private static bool TryString(Dictionary<string, object?> values, string key, out string value)
    {
        if (values.TryGetValue(key, out object? raw) && raw is string text)
        {
            value = text;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool TryInteger(Dictionary<string, object?> values, string key, out int value)
    {
        if (values.TryGetValue(key, out object? raw)
            && raw is double number
            && number >= int.MinValue
            && number <= int.MaxValue
            && Math.Truncate(number) == number)
        {
            value = (int)number;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryStringArray(
        Dictionary<string, object?> values,
        string key,
        List<string> destination)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is not List<object?> items)
        {
            return false;
        }
        foreach (object? item in items)
        {
            if (item is not string text) return false;
            destination.Add(text);
        }
        return true;
    }
}
