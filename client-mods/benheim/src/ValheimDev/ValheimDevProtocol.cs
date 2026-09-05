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
    internal string ChangeId { get; set; } = string.Empty;
    internal string? ExpectedOperationId { get; set; }
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

internal sealed class ValheimDevChangeSummary
{
    internal string ChangeId { get; set; } = string.Empty;
    internal string OperationId { get; set; } = string.Empty;
    internal string SourceSha256 { get; set; } = string.Empty;
    internal string AssemblySha256 { get; set; } = string.Empty;
    internal string InstalledUtc { get; set; } = string.Empty;
    internal string? Result { get; set; }
    internal string CleanupState { get; set; } = ValheimDevCleanupState.Active;
}

internal static class ValheimDevCleanupState
{
    internal const string NotApplicable = "not_applicable";
    internal const string Active = "active";
    internal const string Cleaned = "cleaned";
    internal const string Restored = "restored";
    internal const string RestartRequired = "restart_required";
}

internal sealed class ValheimDevResponse
{
    internal int Protocol { get; set; } = ValheimDevProtocol.ProtocolVersion;
    internal bool Ok { get; set; }
    internal string? Error { get; set; }
    internal ValheimDevBuildIdentity Identity { get; set; } = new ValheimDevBuildIdentity();
    internal bool Authorized { get; set; }
    internal bool RestartRequired { get; set; }
    internal string Action { get; set; } = string.Empty;
    internal string OperationId { get; set; } = string.Empty;
    internal string ChangeId { get; set; } = string.Empty;
    internal string StartedUtc { get; set; } = string.Empty;
    internal string FinishedUtc { get; set; } = string.Empty;
    internal string? Result { get; set; }
    internal string? Exception { get; set; }
    internal string CleanupState { get; set; } = ValheimDevCleanupState.NotApplicable;
    internal bool PreviousChangePreserved { get; set; }
    internal bool EvidenceSelected { get; set; }
    internal bool EvidenceExhaustive { get; set; }
    internal bool EvidenceTruncated { get; set; }
    internal int DroppedEvidenceEvents { get; set; }
    internal List<string> EvidenceEvents { get; } = new List<string>();
    internal List<ValheimDevChangeSummary> ActiveChanges { get; } = new List<ValheimDevChangeSummary>();

    internal string ToJson(bool includeOperation)
    {
        StringBuilder builder = new StringBuilder(1024);
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
        builder.Append(',');
        ValheimDevJson.AppendProperty(builder, "restart_required", RestartRequired);
        builder.Append(',');
        AppendActiveChanges(builder, ActiveChanges);
        if (includeOperation)
        {
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "action", Action);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "operation_id", OperationId);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "change_id", ChangeId);
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
            ValheimDevJson.AppendProperty(builder, "previous_change_preserved", PreviousChangePreserved);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "evidence_selected", EvidenceSelected);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "evidence_exhaustive", EvidenceExhaustive);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "evidence_truncated", EvidenceTruncated);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "dropped_evidence_events", DroppedEvidenceEvents);
            builder.Append(',');
            ValheimDevJson.AppendStringArrayProperty(builder, "evidence_events", EvidenceEvents);
        }
        return builder.Append('}').ToString();
    }

    private static void AppendActiveChanges(StringBuilder builder, List<ValheimDevChangeSummary> changes)
    {
        builder.Append("\"active_changes\":[");
        for (int index = 0; index < changes.Count; index++)
        {
            if (index > 0) builder.Append(',');
            ValheimDevChangeSummary change = changes[index];
            builder.Append('{');
            ValheimDevJson.AppendProperty(builder, "change_id", change.ChangeId);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "operation_id", change.OperationId);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "source_sha256", change.SourceSha256);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "assembly_sha256", change.AssemblySha256);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "installed_utc", change.InstalledUtc);
            builder.Append(',');
            ValheimDevJson.AppendNullableProperty(builder, "result", change.Result);
            builder.Append(',');
            ValheimDevJson.AppendProperty(builder, "cleanup_state", change.CleanupState);
            builder.Append('}');
        }
        builder.Append(']');
    }
}

internal static class ValheimDevProtocol
{
    internal const int ProtocolVersion = 2;
    internal const int MaximumSourceBytes = 256 * 1024;
    internal const int MaximumAssemblyBytes = 1024 * 1024;
    internal const int MaximumRequestBytes = 2 * 1024 * 1024;
    internal const int MaximumQueueDepth = 8;
    internal const int MaximumEvidenceEvents = 64;
    internal const int MaximumEvidenceBytes = 256 * 1024;
    internal const int MaximumEvidenceTimeoutMs = 120000;
    internal const int MaximumJsonDepth = 16;
    internal const string InspectionEntryType = "ValheimDevInspection";
    internal const string ChangeEntryType = "ValheimDevChange";

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
        if (kind != "status" && kind != "inspect" && kind != "install_change" && kind != "remove_change")
        {
            error = "unsupported_request_kind";
            return false;
        }
        if (!HasOnlyAllowedKeys(values, kind))
        {
            error = "unexpected_request_field";
            return false;
        }
        request.Kind = kind;
        request.Protocol = protocol;
        request.Token = token;
        request.Generation = generation;
        if (kind == "status") return true;

        if (!TryString(values, "operation_id", out string operationId) || !ValidIdentifier(operationId))
        {
            error = "invalid_operation_id";
            return false;
        }
        request.OperationId = operationId;
        if (kind == "install_change" || kind == "remove_change")
        {
            if (!TryString(values, "change_id", out string changeId) || !ValidIdentifier(changeId))
            {
                error = "invalid_change_id";
                return false;
            }
            request.ChangeId = changeId;
            if (!TryNullableString(values, "expected_operation_id", out string? expectedOperationId)
                || (expectedOperationId != null && !ValidIdentifier(expectedOperationId)))
            {
                error = "invalid_expected_operation_id";
                return false;
            }
            request.ExpectedOperationId = expectedOperationId;
        }
        if (kind == "remove_change") return true;

        if (!TryString(values, "source", out string source)
            || !TryString(values, "source_sha256", out string sourceSha256)
            || !TryString(values, "assembly_sha256", out string assemblySha256)
            || !TryString(values, "assembly", out string assemblyBase64)
            || !TryString(values, "entry_type", out string entryType)
            || !TryInteger(values, "evidence_timeout_ms", out int timeoutMs)
            || !TryStringArray(values, "evidence_events", request.EvidenceEvents))
        {
            error = "missing_code_fields";
            return false;
        }
        if (string.IsNullOrWhiteSpace(source) || Encoding.UTF8.GetByteCount(source) > MaximumSourceBytes)
        {
            error = "source_invalid";
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
        if (!ValidEvidenceSelectors(request.EvidenceEvents, out error)) return false;
        request.Source = source;
        request.SourceSha256 = sourceSha256;
        request.AssemblySha256 = assemblySha256;
        request.AssemblyBase64 = assemblyBase64;
        request.EntryType = entryType;
        request.EvidenceTimeoutMs = timeoutMs;
        return true;
    }

    private static bool ValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
        foreach (char character in value)
        {
            bool asciiLetter = (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z');
            bool asciiDigit = character >= '0' && character <= '9';
            if (!(asciiLetter || asciiDigit || character == '-' || character == '_' || character == '.')) return false;
        }
        return true;
    }

    private static bool HasOnlyAllowedKeys(Dictionary<string, object?> values, string kind)
    {
        foreach (string key in values.Keys)
        {
            bool envelope = key == "kind" || key == "protocol" || key == "token" || key == "generation";
            if (envelope) continue;
            if (kind == "status") return false;
            if (key == "operation_id") continue;
            if ((kind == "install_change" || kind == "remove_change")
                && (key == "change_id" || key == "expected_operation_id")) continue;
            if (kind == "remove_change") return false;
            if (key == "source" || key == "source_sha256" || key == "assembly_sha256"
                || key == "assembly" || key == "entry_type" || key == "evidence_events"
                || key == "evidence_timeout_ms") continue;
            return false;
        }
        return true;
    }

    private static bool ValidEvidenceSelectors(List<string> selectors, out string error)
    {
        error = string.Empty;
        if (selectors.Count > MaximumEvidenceEvents)
        {
            error = "too_many_evidence_selectors";
            return false;
        }
        foreach (string selector in selectors)
        {
            int separator = selector.IndexOf(':');
            if (separator <= 0 || separator == selector.Length - 1 || selector.Length > 128
                || selector.IndexOf(':', separator + 1) >= 0)
            {
                error = "invalid_evidence_selector";
                return false;
            }
            foreach (char character in selector)
            {
                if (char.IsWhiteSpace(character))
                {
                    error = "invalid_evidence_selector";
                    return false;
                }
            }
        }
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

    private static bool TryNullableString(
        Dictionary<string, object?> values,
        string key,
        out string? value)
    {
        if (!values.TryGetValue(key, out object? raw))
        {
            value = null;
            return false;
        }
        if (raw == null || raw is string)
        {
            value = raw as string;
            return true;
        }
        value = null;
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

    private static bool TryStringArray(Dictionary<string, object?> values, string key, List<string> destination)
    {
        if (!values.TryGetValue(key, out object? raw) || raw is not List<object?> items) return false;
        foreach (object? item in items)
        {
            if (item is not string text) return false;
            destination.Add(text);
        }
        return true;
    }
}
