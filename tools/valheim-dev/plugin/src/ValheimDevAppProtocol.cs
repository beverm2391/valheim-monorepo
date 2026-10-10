using System;
using System.Collections.Generic;
using System.Text;

namespace ValheimDev;

internal sealed class ValheimDevAppRequest
{
    internal string AppId { get; set; } = string.Empty;
    internal string RequestId { get; set; } = string.Empty;
    internal string Action { get; set; } = string.Empty;
    internal string Name { get; set; } = string.Empty;
    internal string? Seed { get; set; }
    internal string World { get; set; } = string.Empty;
    internal string Character { get; set; } = string.Empty;
}

internal static class ValheimDevAppProtocol
{
    internal const int Version = 1;
    internal const int MaximumRequestBytes = 16384;
    internal const int MaximumResponseBytes = 256 * 1024;
    internal const int MaximumQueueDepth = 8;

    internal static bool IsDisposableName(string name)
    {
        if (name.Length < 5 || name.Length > 48 || !name.StartsWith("Lab-", StringComparison.Ordinal)) return false;
        for (int index = 4; index < name.Length; index++)
        {
            char c = name[index];
            if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_' || c == '-')) return false;
        }
        return true;
    }

    internal static bool TryParse(string json, out ValheimDevAppRequest request, out string error)
    {
        request = new ValheimDevAppRequest();
        error = string.Empty;
        if (Encoding.UTF8.GetByteCount(json) > MaximumRequestBytes) { error = "request_too_large"; return false; }
        if (!ValheimDevJson.TryParseObject(json, out Dictionary<string, object?> values, out _))
        { error = "invalid_json"; return false; }
        request.AppId = String(values, "app_id") ?? string.Empty;
        request.RequestId = String(values, "request_id") ?? string.Empty;
        request.Action = String(values, "action") ?? string.Empty;
        if (!values.TryGetValue("protocol", out object? version) || version is not double number || number != Version)
        { error = "protocol_mismatch"; return false; }
        if (!Guid.TryParse(request.AppId, out _) || !Guid.TryParse(request.RequestId, out _))
        { error = "invalid_request_identity"; return false; }
        string action = request.Action;
        if (action != "status" && action != "list_saves" && action != "create_world"
            && action != "create_character" && action != "open_lab" && action != "close_lab")
        { error = "unsupported_action"; return false; }
        foreach (string key in values.Keys)
        {
            if (key == "protocol" || key == "app_id" || key == "request_id" || key == "action") continue;
            if ((action == "create_world" || action == "create_character") && key == "name") continue;
            if (action == "create_world" && key == "seed") continue;
            if (action == "open_lab" && (key == "world" || key == "character")) continue;
            error = "unexpected_request_field"; return false;
        }
        if (action == "create_world" || action == "create_character")
        {
            request.Name = String(values, "name") ?? string.Empty;
            if (!IsDisposableName(request.Name)) { error = "invalid_disposable_name"; return false; }
            if (values.ContainsKey("seed"))
            {
                request.Seed = String(values, "seed");
                if (request.Seed == null || request.Seed.Length > 10) { error = "invalid_seed"; return false; }
                foreach (char c in request.Seed)
                    if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9'))
                    { error = "invalid_seed"; return false; }
            }
        }
        if (action == "open_lab")
        {
            request.World = String(values, "world") ?? string.Empty;
            request.Character = String(values, "character") ?? string.Empty;
            if (!IsDisposableName(request.World) || !IsDisposableName(request.Character))
            { error = "invalid_disposable_name"; return false; }
        }
        return true;
    }

    private static string? String(Dictionary<string, object?> values, string key) =>
        values.TryGetValue(key, out object? raw) ? raw as string : null;

    internal static string Response(string appId, string requestId, string? error, string result = "{}")
    {
        StringBuilder json = new StringBuilder("{");
        ValheimDevJson.AppendProperty(json, "protocol", Version);
        json.Append(','); ValheimDevJson.AppendProperty(json, "app_id", appId);
        json.Append(','); ValheimDevJson.AppendProperty(json, "request_id", requestId);
        json.Append(','); ValheimDevJson.AppendProperty(json, "ok", error == null);
        json.Append(','); ValheimDevJson.AppendNullableProperty(json, "error", error);
        json.Append(",\"result\":").Append(result).Append('}');
        if (Encoding.UTF8.GetByteCount(json.ToString()) > MaximumResponseBytes)
            return Response(appId, requestId, "response_too_large");
        return json.ToString();
    }
}
