using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BenheimQoL.ValheimDev;

internal static partial class Program
{
    private static void GateMatrixAndRespawnPreservation()
    {
        ValheimDevWorldState eligible = EligibleState();
        Require(ValheimDevEligibility.CheckAuthorization(eligible) == "eligible", "eligible local world passes");
        EachGate(eligible, value => value.Network = null, "no_network");
        EachGate(eligible, value => value.Scene = null, "no_world_scene");
        EachGate(eligible, value => value.GameplayHooksHealthy = false, "gameplay_hooks_unhealthy");
        EachGate(eligible, value => value.IsServer = false, "not_server");
        EachGate(eligible, value => value.IsOpenServer = true, "open_server");
        EachGate(eligible, value => value.IsDedicated = true, "dedicated_server");
        EachGate(eligible, value => value.PeerCount = 1, "peers_present");
        EachGate(eligible, value => value.HasServerRpc = true, "server_rpc_present");
        EachGate(eligible, value => value.LocalPlayer = null, "no_local_player");
        EachGate(eligible, value => value.LocalPlayerIsAlive = false, "no_local_player");
        EachGate(eligible, value => value.LocalPlayerIsOwner = false, "local_player_not_owned");

        ValheimDevWorldCapture capture = new ValheimDevWorldCapture
        {
            Network = eligible.Network!,
            Scene = eligible.Scene!,
            WorldId = eligible.WorldId
        };
        eligible.LocalPlayer = new object();
        Require(ValheimDevEligibility.CheckOperation(capture, eligible) == "eligible", "respawn replacement remains eligible");
        EachSessionGate(capture, eligible, value => value.Network = new object(), "network_changed");
        EachSessionGate(capture, eligible, value => value.Scene = new object(), "scene_changed");
        EachSessionGate(capture, eligible, value => value.WorldId++, "world_changed");
    }

    private static void ProtocolBounds()
    {
        string request = CodeProtocolRequest("inspect", new string('s', ValheimDevProtocol.MaximumSourceBytes + 1), "op", string.Empty, 0);
        Require(!ValheimDevProtocol.TryParseRequest(request, out _, out string error)
            && error == "source_invalid", "oversized source is rejected before enqueue");

        List<string> tooMany = new List<string>();
        for (int index = 0; index <= ValheimDevProtocol.MaximumEvidenceEvents; index++) tooMany.Add("D:E" + index);
        Dictionary<string, object?> fields = CodeFields("install_change", "x", "op", "affinity.icon", 0);
        fields["evidence_events"] = tooMany;
        Require(!ValheimDevProtocol.TryParseRequest(JsonSerializer.Serialize(fields), out _, out error)
            && error == "too_many_evidence_selectors", "selector count is bounded");

        Require(ValheimDevProtocol.MaximumEvidenceTimeoutMs == 120000,
            "runtime advertises the reviewed two-minute evidence ceiling");
        request = CodeProtocolRequest("inspect", "x", "op", string.Empty, 120000);
        Require(ValheimDevProtocol.TryParseRequest(request, out ValheimDevRequest maximumTimeout, out error)
            && maximumTimeout.EvidenceTimeoutMs == 120000, "maximum evidence timeout is accepted");

        string fractional = request.Replace("120000", "1.5", StringComparison.Ordinal);
        Require(!ValheimDevProtocol.TryParseRequest(fractional, out _, out error)
            && error == "missing_code_fields", "fractional protocol integers are rejected");
        Require(!ValheimDevProtocol.TryParseRequest(
                "{\"kind\":\"remove_change\",\"protocol\":2,\"token\":\"t\",\"generation\":\"g\",\"operation_id\":\"op\",\"change_id\":\"bad id\"}",
                out _, out error)
            && error == "invalid_change_id", "change identifiers are bounded protocol values");
        Require(!ValheimDevProtocol.TryParseRequest(
                "{\"kind\":\"remove_change\",\"protocol\":2,\"token\":\"t\",\"generation\":\"g\",\"operation_id\":\"op\",\"change_id\":\"caf\\u00e9\"}",
                out _, out error)
            && error == "invalid_change_id", "change identifiers use the same ASCII grammar as the MCP schema");
        Require(!ValheimDevProtocol.TryParseRequest(
                "{\"kind\":\"remove_change\",\"protocol\":2,\"token\":\"t\",\"generation\":\"g\",\"operation_id\":\"op\",\"change_id\":\"affinity.icon\"}",
                out _, out error)
            && error == "invalid_expected_operation_id", "mutations require an explicit expected prior version or absence");
        Require(!ValheimDevProtocol.TryParseRequest(
                "{\"kind\":\"status\",\"protocol\":2,\"token\":\"t\",\"generation\":\"g\",\"extra\":true}",
                out _, out error)
            && error == "unexpected_request_field", "request kinds reject unexpected fields");

        Dictionary<string, object?> badSelector = CodeFields("inspect", "x", "op", string.Empty, 0);
        badSelector["evidence_events"] = new[] { "A:B:C" };
        Require(!ValheimDevProtocol.TryParseRequest(JsonSerializer.Serialize(badSelector), out _, out error)
            && error == "invalid_evidence_selector", "selectors contain exactly one separator");
        badSelector["evidence_events"] = new[] { "A:bad event" };
        Require(!ValheimDevProtocol.TryParseRequest(JsonSerializer.Serialize(badSelector), out _, out error)
            && error == "invalid_evidence_selector", "selectors reject whitespace");

        string unicodeEnvelope = "{\"kind\":\"stat\\u0075s\",\"protocol\":2,\"token\":\"\\u0074\",\"generation\":\"g\"}";
        Require(ValheimDevProtocol.TryParseRequest(unicodeEnvelope, out ValheimDevRequest unicode, out error)
            && unicode.Kind == "status" && unicode.Token == "t", "Unicode escapes decode in protocol strings");
        Require(!ValheimDevProtocol.TryParseRequest(
                "{\"kind\":\"status\",\"protocol\":2,\"token\":\"t\",\"generation\":\"g\",}",
                out _, out error)
            && error.StartsWith("invalid_json:", StringComparison.Ordinal), "malformed JSON is rejected");

        string deepValue = new string('[', ValheimDevProtocol.MaximumJsonDepth) + "0"
            + new string(']', ValheimDevProtocol.MaximumJsonDepth);
        string deeplyNested = "{\"kind\":\"status\",\"protocol\":2,\"token\":\"t\",\"generation\":\"g\",\"extra\":"
            + deepValue + "}";
        Require(!ValheimDevProtocol.TryParseRequest(deeplyNested, out _, out error)
            && error.Contains("nesting exceeds", StringComparison.Ordinal), "deep JSON is rejected before validation");
    }

    private static void ExecutorVariants(string goodPath, string throwingPath, string badPath)
    {
        byte[] good = File.ReadAllBytes(goodPath);
        ValheimDevExecutionResult first = ValheimDevCodeExecutor.Prepare(
            good, ValheimDevProtocol.ChangeEntryType, requireCleanup: true);
        ValheimDevExecutionResult second = ValheimDevCodeExecutor.Prepare(
            good, ValheimDevProtocol.ChangeEntryType, requireCleanup: true);
        Require(first.Ok && second.Ok && first.LoadedCode != null && second.LoadedCode != null,
            "managed change entrypoint prepares repeatedly");
        Require(!ReferenceEquals(first.LoadedCode!.Run.Module.Assembly, second.LoadedCode!.Run.Module.Assembly),
            "each preparation receives a distinct loaded assembly");

        ValheimDevExecutionResult preparedThrowing = ValheimDevCodeExecutor.Prepare(
            File.ReadAllBytes(throwingPath), ValheimDevProtocol.ChangeEntryType, requireCleanup: true);
        ValheimDevExecutionResult throwing = ValheimDevCodeExecutor.Invoke(preparedThrowing.LoadedCode!);
        Require(!throwing.Ok && throwing.Error == "entrypoint_exception"
            && throwing.Exception!.Contains("change exploded", StringComparison.Ordinal), "change exceptions are returned");
        ValheimDevExecutionResult bad = ValheimDevCodeExecutor.Prepare(
            File.ReadAllBytes(badPath), ValheimDevProtocol.ChangeEntryType, requireCleanup: false);
        Require(!bad.Ok && bad.Error == "run_entrypoint_invalid", "bad entrypoint is rejected");
    }

    private static string CodeProtocolRequest(string kind, string source, string operationId, string changeId, int timeout)
    {
        return JsonSerializer.Serialize(CodeFields(kind, source, operationId, changeId, timeout));
    }

    private static Dictionary<string, object?> CodeFields(
        string kind,
        string source,
        string operationId,
        string changeId,
        int timeout)
    {
        Dictionary<string, object?> fields = new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["protocol"] = 2,
            ["token"] = "t",
            ["generation"] = "g",
            ["operation_id"] = operationId,
            ["source"] = source,
            ["source_sha256"] = new string('0', 64),
            ["assembly_sha256"] = new string('0', 64),
            ["assembly"] = "AA==",
            ["entry_type"] = kind == "inspect" ? "ValheimDevInspection" : "ValheimDevChange",
            ["evidence_events"] = Array.Empty<string>(),
            ["evidence_timeout_ms"] = timeout
        };
        if (!string.IsNullOrEmpty(changeId))
        {
            fields["change_id"] = changeId;
            fields["expected_operation_id"] = null;
        }
        return fields;
    }
}
