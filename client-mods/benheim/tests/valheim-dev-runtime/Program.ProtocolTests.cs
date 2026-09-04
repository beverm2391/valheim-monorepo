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
        object replacementPlayer = new object();
        eligible.LocalPlayer = replacementPlayer;
        Require(ValheimDevEligibility.CheckOperation(capture, eligible) == "eligible", "respawn replacement remains eligible");
        EachSessionGate(capture, eligible, value => value.Network = new object(), "network_changed");
        EachSessionGate(capture, eligible, value => value.Scene = new object(), "scene_changed");
        EachSessionGate(capture, eligible, value => value.WorldId++, "world_changed");
    }

    private static void ProtocolBounds()
    {
        string oversized = new string('s', ValheimDevProtocol.MaximumSourceBytes + 1);
        string request = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "apply",
            ["protocol"] = 1,
            ["token"] = "t",
            ["generation"] = "g",
            ["operation_id"] = "op",
            ["source"] = oversized,
            ["source_sha256"] = new string('0', 64),
            ["assembly_sha256"] = new string('0', 64),
            ["assembly"] = "AA==",
            ["entry_type"] = "ValheimDevExperiment",
            ["evidence_events"] = Array.Empty<string>(),
            ["evidence_timeout_ms"] = 0
        });
        Require(!ValheimDevProtocol.TryParseRequest(request, out _, out string error)
            && error == "source_too_large", "oversized source is rejected before enqueue");

        string assemblyTooLarge = new string('A', ((ValheimDevProtocol.MaximumAssemblyBytes + 2) / 3) * 4 + 9);
        request = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "apply",
            ["protocol"] = 1,
            ["token"] = "t",
            ["generation"] = "g",
            ["operation_id"] = "op",
            ["source"] = "x",
            ["source_sha256"] = new string('0', 64),
            ["assembly_sha256"] = new string('0', 64),
            ["assembly"] = assemblyTooLarge,
            ["entry_type"] = "ValheimDevExperiment",
            ["evidence_events"] = Array.Empty<string>(),
            ["evidence_timeout_ms"] = 0
        });
        Require(!ValheimDevProtocol.TryParseRequest(request, out _, out error)
            && error == "assembly_too_large", "oversized assembly is rejected before enqueue");

        List<string> tooMany = new List<string>();
        for (int index = 0; index <= ValheimDevProtocol.MaximumEvidenceEvents; index++) tooMany.Add("D:E" + index);
        request = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "apply",
            ["protocol"] = 1,
            ["token"] = "t",
            ["generation"] = "g",
            ["operation_id"] = "op",
            ["source"] = "x",
            ["source_sha256"] = new string('0', 64),
            ["assembly_sha256"] = new string('0', 64),
            ["assembly"] = "AA==",
            ["entry_type"] = "ValheimDevExperiment",
            ["evidence_events"] = tooMany,
            ["evidence_timeout_ms"] = 0
        });
        Require(!ValheimDevProtocol.TryParseRequest(request, out _, out error)
            && error == "too_many_evidence_selectors", "selector count is bounded");

        Require(ValheimDevProtocol.MaximumEvidenceTimeoutMs == 120000,
            "runtime advertises the reviewed two-minute evidence ceiling");
        request = MinimalApplyProtocolRequest("120000");
        Require(ValheimDevProtocol.TryParseRequest(request, out ValheimDevRequest maximumTimeout, out error)
            && maximumTimeout.EvidenceTimeoutMs == 120000,
            "maximum evidence timeout is accepted as an integer");

        request = MinimalApplyProtocolRequest("1.5");
        Require(!ValheimDevProtocol.TryParseRequest(request, out _, out error)
            && error == "missing_apply_fields",
            "fractional protocol integers are rejected");
        request = MinimalApplyProtocolRequest("01");
        Require(!ValheimDevProtocol.TryParseRequest(request, out _, out error)
            && error.StartsWith("invalid_json:", StringComparison.Ordinal),
            "non-conforming JSON numbers are rejected");

        string unicodeEnvelope = "{\"kind\":\"stat\\u0075s\",\"protocol\":1,\"token\":\"\\u0074\",\"generation\":\"g\"}";
        Require(ValheimDevProtocol.TryParseRequest(unicodeEnvelope, out ValheimDevRequest unicode, out error)
            && unicode.Kind == "status" && unicode.Token == "t",
            "Unicode escapes decode in protocol strings");
        Require(!ValheimDevProtocol.TryParseRequest(
                "{\"kind\":\"status\",\"protocol\":1,\"token\":\"\\u00xz\",\"generation\":\"g\"}",
                out _, out error)
            && error.StartsWith("invalid_json:", StringComparison.Ordinal),
            "malformed Unicode escapes are rejected");
        Require(!ValheimDevProtocol.TryParseRequest(
                "{\"kind\":\"status\",\"protocol\":1,\"token\":\"t\",\"generation\":\"g\",}",
                out _, out error)
            && error.StartsWith("invalid_json:", StringComparison.Ordinal),
            "malformed JSON is rejected");

        string deepValue = new string('[', ValheimDevProtocol.MaximumJsonDepth)
            + "0"
            + new string(']', ValheimDevProtocol.MaximumJsonDepth);
        string deeplyNested = "{\"kind\":\"status\",\"protocol\":1,\"token\":\"t\",\"generation\":\"g\",\"extra\":"
            + deepValue + "}";
        Require(!ValheimDevProtocol.TryParseRequest(deeplyNested, out _, out error)
            && error.Contains("nesting exceeds", StringComparison.Ordinal),
            "deep JSON is rejected by the parser before request validation");
    }

    private static void ExecutorVariants(string goodPath, string throwingPath, string badPath)
    {
        byte[] good = File.ReadAllBytes(goodPath);
        ValheimDevExecutionResult first = ValheimDevExperimentExecutor.Execute(good, ValheimDevProtocol.ExpectedEntryType);
        ValheimDevExecutionResult second = ValheimDevExperimentExecutor.Execute(good, ValheimDevProtocol.ExpectedEntryType);
        Require(first.Ok && second.Ok && first.Result == "good-result" && second.Result == "good-result", "unique assembly loads execute repeatedly");
        Require(first.LoadedExperiment != null && second.LoadedExperiment != null
            && !ReferenceEquals(first.LoadedExperiment.Cleanup!.Module.Assembly, second.LoadedExperiment.Cleanup!.Module.Assembly),
            "each apply receives a distinct loaded assembly");

        ValheimDevExecutionResult throwing = ValheimDevExperimentExecutor.Execute(
            File.ReadAllBytes(throwingPath),
            ValheimDevProtocol.ExpectedEntryType);
        Require(!throwing.Ok && throwing.Error == "experiment_exception"
            && throwing.Exception!.Contains("experiment exploded", StringComparison.Ordinal),
            "experiment exceptions are returned");
        ValheimDevExecutionResult bad = ValheimDevExperimentExecutor.Execute(
            File.ReadAllBytes(badPath),
            ValheimDevProtocol.ExpectedEntryType);
        Require(!bad.Ok && bad.Error == "run_entrypoint_invalid", "bad entrypoint is rejected");
    }
}
