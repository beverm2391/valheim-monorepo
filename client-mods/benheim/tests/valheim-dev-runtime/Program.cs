using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BenheimQoL.Infrastructure;
using BenheimQoL.ValheimDev;

internal static partial class Program
{
    private static ValheimDevWorldState state = EligibleState();
    private static string token = string.Empty;
    private static string generation = string.Empty;
    private static int port;
    private static string root = string.Empty;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 5) throw new InvalidOperationException("five fixture assemblies are required");
            GateMatrixAndRespawnPreservation();
            ProtocolBounds();
            ExecutorVariants(args[0], args[2], args[3]);
            RuntimeLifecycle(args);
            Console.WriteLine("Valheim Dev runtime behavior checks passed");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            ValheimDevRuntime.Revoke("test_end");
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
    private static void RuntimeLifecycle(string[] fixtures)
    {
        root = Path.Combine(Path.GetTempPath(), "benheim-valheim-dev-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "ValheimDev"));
        File.WriteAllText(Path.Combine(root, "ValheimDev", "session.json"), "stale");
        ValheimDevRuntime.SetTestHooks(() => state, TestIdentity);
        ResetRuntime();
        Require(!File.Exists(ValheimDevRuntime.DescriptorPath), "startup removes stale descriptor");

        Authorize();
        string firstToken = token;
        string firstGeneration = generation;
        Require(Status().GetProperty("active_changes").GetArrayLength() == 0, "status starts with no managed changes");

        Task<string> wrongThreadInspection = SendAsync(CodeRequest(
            "inspect", "wrong-thread", string.Empty, fixtures[0], Array.Empty<string>(), 0));
        WaitForQueue();
        Thread wrong = new Thread(ValheimDevRuntime.Update);
        wrong.Start();
        wrong.Join();
        Thread.Sleep(25);
        Require(!wrongThreadInspection.IsCompleted, "only Unity's main thread can inspect the live runtime");
        Require(Parse(Pump(wrongThreadInspection)).GetProperty("ok").GetBoolean(),
            "main-thread Update completes the queued inspection");

        ValheimDevTestSurface.Reset();
        JsonElement inspection = Inspect("inspect-affinity-icon", fixtures[0]);
        Require(inspection.GetProperty("ok").GetBoolean()
            && inspection.GetProperty("result").GetString()!.Contains("Affinity.weapon_icon", StringComparison.Ordinal)
            && inspection.GetProperty("cleanup_state").GetString() == "not_applicable"
            && !ValheimDevTestSurface.Visible,
            "inspection describes the live icon surface without installing a change");

        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "pulse-a");
        Task<string> selectedTask = SendAsync(CodeRequest(
            "install_change", "install-a", "affinity.weapon-icon", fixtures[0],
            new[] { "Affinity:weapon_icon_observed" }, 5000));
        WaitForQueue();
        ValheimDevRuntime.Update();
        Require(!selectedTask.IsCompleted && ValheimDevTestSurface.Visible
            && ValheimDevTestSurface.Variant == "pulse-a",
            "managed change remains active while selected evidence is observed");
        Diagnostics.Emit(DiagnosticEvent.Create("Affinity", "weapon_icon_observed").String("variant", "pulse-a"));
        JsonElement installed = Parse(Pump(selectedTask));
        Require(installed.GetProperty("ok").GetBoolean()
            && installed.GetProperty("cleanup_state").GetString() == "active"
            && installed.GetProperty("evidence_events").GetArrayLength() == 1
            && installed.GetProperty("active_changes").GetArrayLength() == 1
            && ValheimDevTestSurface.Visible,
            "install returns selected evidence while preserving the visible change");

        JsonElement status = Status();
        JsonElement active = status.GetProperty("active_changes")[0];
        Require(active.GetProperty("change_id").GetString() == "affinity.weapon-icon"
            && active.GetProperty("operation_id").GetString() == "install-a",
            "status reports the active managed change and owning operation");

        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "pulse-b");
        JsonElement replaced = Install("replace-b", "affinity.weapon-icon", fixtures[0]);
        Require(replaced.GetProperty("ok").GetBoolean()
            && replaced.GetProperty("cleanup_state").GetString() == "active"
            && ValheimDevTestSurface.Visible && ValheimDevTestSurface.Variant == "pulse-b",
            "replacement cleans the prior version and keeps the new variant active");

        StaleChangeState(fixtures[0]);

        JsonElement failedReplacement = Install("replace-throws", "affinity.weapon-icon", fixtures[2]);
        Require(failedReplacement.GetProperty("error").GetString() == "entrypoint_exception"
            && failedReplacement.GetProperty("cleanup_state").GetString() == "restored"
            && failedReplacement.GetProperty("previous_change_preserved").GetBoolean()
            && ValheimDevTestSurface.Visible && ValheimDevTestSurface.Variant == "pulse-b",
            "runtime failure cleans the candidate and restores the working version");

        JsonElement missingCleanup = Install("replace-no-cleanup", "affinity.weapon-icon", fixtures[1]);
        Require(missingCleanup.GetProperty("error").GetString() == "cleanup_entrypoint_required"
            && missingCleanup.GetProperty("previous_change_preserved").GetBoolean()
            && ValheimDevTestSurface.Visible && ValheimDevTestSurface.Variant == "pulse-b",
            "invalid candidate is rejected before touching the working version");

        JsonElement removed = Remove("remove-icon", "affinity.weapon-icon");
        Require(removed.GetProperty("ok").GetBoolean()
            && removed.GetProperty("cleanup_state").GetString() == "cleaned"
            && removed.GetProperty("active_changes").GetArrayLength() == 0
            && !ValheimDevTestSurface.Visible,
            "remove cleans the visible change and forgets it only after cleanup succeeds");

        EvidenceBoundaries(fixtures[0]);

        Task<string> queued = SendAsync(CodeRequest("inspect", "queued-before-off", string.Empty, fixtures[0], Array.Empty<string>(), 0));
        WaitForQueue();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, new Terminal());
        JsonElement canceled = Parse(queued.GetAwaiter().GetResult());
        Require(canceled.GetProperty("error").GetString() == "authorization_revoked"
            && !ValheimDevRuntime.IsAuthorizedForTests && !File.Exists(ValheimDevRuntime.DescriptorPath),
            "off invalidates the descriptor and cancels queued work");

        Authorize();
        Require(token != firstToken && generation != firstGeneration, "reauthorization rotates token and generation");
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "revoked");
        Require(Install("install-before-off", "affinity.weapon-icon", fixtures[0]).GetProperty("ok").GetBoolean(),
            "managed change installs before explicit revocation");
        ValheimDevRuntime.Revoke("explicit_off");
        Require(!ValheimDevTestSurface.Visible && !File.Exists(ValheimDevRuntime.DescriptorPath),
            "revocation cleans managed changes and removes the descriptor");

        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_CLEANUP_ONCE", "1");
        JsonElement failing = Install("failing-cleanup", "affinity.weapon-icon", fixtures[4]);
        Require(failing.GetProperty("ok").GetBoolean(), "change with a callable cleanup can install");
        JsonElement cleanupFailure = Remove("remove-failing-cleanup", "affinity.weapon-icon");
        Require(cleanupFailure.GetProperty("error").GetString() == "change_cleanup_failed"
            && cleanupFailure.GetProperty("cleanup_state").GetString() == "restart_required",
            "failed cleanup is explicit on the operation that attempted it");
        Require(Status().GetProperty("restart_required").GetBoolean(),
            "status exposes the sticky restart requirement");
        Require(Install("blocked-after-cleanup", "another-change", fixtures[0])
                .GetProperty("error").GetString() == "restart_required",
            "cleanup uncertainty blocks further mutation for the process lifetime");
        Terminal dirtyStatus = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "status" }, dirtyStatus);
        Require(dirtyStatus.Lines[0].Contains("restart is required", StringComparison.Ordinal)
            && dirtyStatus.Lines[0].Contains("affinity.weapon-icon", StringComparison.Ordinal),
            "authorized console status surfaces sticky cleanup uncertainty");
        Terminal dirtyOff = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, dirtyOff);
        Require(dirtyOff.Lines[0].Contains("restart is required", StringComparison.Ordinal)
            && dirtyOff.Lines[0].Contains("affinity.weapon-icon", StringComparison.Ordinal),
            "off preserves sticky restart reporting after a cleanup retry succeeds");

        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_CLEANUP_ONCE", "1");
        Require(Install("dirty-before-drift", "affinity.weapon-icon", fixtures[4]).GetProperty("ok").GetBoolean(),
            "drift proof installs a fail-once cleanup change");
        Require(Remove("dirty-remove-before-drift", "affinity.weapon-icon")
                .GetProperty("cleanup_state").GetString() == "restart_required",
            "drift proof establishes sticky cleanup uncertainty");
        state.Scene = new object();
        Terminal dirtyDriftStatus = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "status" }, dirtyDriftStatus);
        Require(dirtyDriftStatus.Lines[0].Contains("restart is required", StringComparison.Ordinal)
            && dirtyDriftStatus.Lines[0].Contains("affinity.weapon-icon", StringComparison.Ordinal),
            "drift-triggered status preserves sticky restart reporting after cleanup retry succeeds");

        ResetRuntime();
        Authorize();
        Require(Install("old-failing-cleanup", "affinity.weapon-icon", fixtures[4]).GetProperty("ok").GetBoolean(),
            "failing-cleanup fixture can become the working version");
        JsonElement previousCleanupFailed = Install("replace-after-cleanup-failure", "affinity.weapon-icon", fixtures[0]);
        Require(previousCleanupFailed.GetProperty("error").GetString() == "previous_change_cleanup_failed"
            && previousCleanupFailed.GetProperty("cleanup_state").GetString() == "restart_required"
            && !ValheimDevTestSurface.Visible,
            "replacement stops before running the candidate when prior cleanup fails");

        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_RUN", "1");
        JsonElement candidateCleanupFailed = Install("candidate-cleanup-failure", "affinity.weapon-icon", fixtures[4]);
        Require(candidateCleanupFailed.GetProperty("error").GetString() == "entrypoint_exception"
            && candidateCleanupFailed.GetProperty("cleanup_state").GetString() == "restart_required"
            && candidateCleanupFailed.GetProperty("active_changes")[0].GetProperty("cleanup_state").GetString() == "restart_required",
            "failed candidate cleanup leaves explicit uncertain managed state");

        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_ON_RESTORE", "1");
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "restore-failure");
        Require(Install("working-before-restore-failure", "affinity.weapon-icon", fixtures[0]).GetProperty("ok").GetBoolean(),
            "working version installs before restoration failure proof");
        JsonElement restoreFailed = Install("restore-failure", "affinity.weapon-icon", fixtures[2]);
        Require(restoreFailed.GetProperty("error").GetString() == "previous_change_restore_failed"
            && restoreFailed.GetProperty("cleanup_state").GetString() == "restart_required"
            && restoreFailed.GetProperty("active_changes")[0].GetProperty("cleanup_state").GetString() == "restart_required",
            "failed restoration is explicit and requires restart");

        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "mixed-cleanup");
        Require(Install("mixed-good", "affinity.weapon-icon", fixtures[0]).GetProperty("ok").GetBoolean(),
            "mixed cleanup proof installs a cleanable change");
        Require(Install("mixed-failing", "other-change", fixtures[4]).GetProperty("ok").GetBoolean(),
            "mixed cleanup proof installs an uncertain-cleanup change");
        Task<string> inspectingDuringRevoke = SendAsync(CodeRequest(
            "inspect", "inspect-during-revoke", string.Empty, fixtures[0], new[] { "Test:not-emitted" }, 5000));
        WaitForQueue();
        ValheimDevRuntime.Update();
        Terminal off = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, off);
        JsonElement revokedInspection = Parse(inspectingDuringRevoke.GetAwaiter().GetResult());
        Require(revokedInspection.GetProperty("cleanup_state").GetString() == "not_applicable"
            && revokedInspection.GetProperty("restart_required").GetBoolean()
            && !ValheimDevTestSurface.Visible
            && off.Lines[0].Contains("restart is required", StringComparison.Ordinal)
            && off.Lines[0].Contains("other-change", StringComparison.Ordinal),
            "revocation reports mixed cleanup globally without assigning it to an inspection");
        Terminal offStatus = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "status" }, offStatus);
        Require(offStatus.Lines[0].Contains("restart is required", StringComparison.Ordinal)
            && offStatus.Lines[0].Contains("other-change", StringComparison.Ordinal),
            "console status preserves cleanup uncertainty after authorization is gone");

        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "world-drift");
        Install("install-before-world-drift", "affinity.weapon-icon", fixtures[0]);
        state.Scene = new object();
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && !File.Exists(ValheimDevRuntime.DescriptorPath)
            && !ValheimDevTestSurface.Visible,
            "world identity drift revokes authorization and cleans active changes");
    }

    private static void ResetRuntime()
    {
        ValheimDevRuntime.Revoke("simulated_process_restart");
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_RUN", null);
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_ON_RESTORE", null);
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_CLEANUP_ONCE", null);
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", null);
        state = EligibleState();
        ValheimDevTestSurface.Reset();
        ValheimDevRuntime.Initialize(root, "test-benheim", Thread.CurrentThread.ManagedThreadId);
    }

    private static JsonElement Status()
    {
        return Parse(Pump(SendAsync(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "status", ["protocol"] = 2, ["token"] = token, ["generation"] = generation
        }))));
    }

    private static JsonElement Inspect(string operationId, string assemblyPath)
        => Parse(Pump(SendAsync(CodeRequest("inspect", operationId, string.Empty, assemblyPath, Array.Empty<string>(), 0))));

    private static JsonElement Install(string operationId, string changeId, string assemblyPath)
        => Parse(Pump(SendAsync(CodeRequest("install_change", operationId, changeId, assemblyPath, Array.Empty<string>(), 0))));

    private static JsonElement Remove(
        string operationId,
        string changeId,
        string? expectedOperationIdOverride = null)
    {
        return Parse(Pump(SendAsync(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "remove_change", ["protocol"] = 2, ["token"] = token,
            ["generation"] = generation, ["operation_id"] = operationId, ["change_id"] = changeId,
            ["expected_operation_id"] = expectedOperationIdOverride ?? ActiveOperationId(changeId)
        }))));
    }

    private static string CodeRequest(
        string kind,
        string operationId,
        string changeId,
        string assemblyPath,
        string[] selectors,
        int timeoutMs,
        string? expectedOperationIdOverride = null)
    {
        byte[] assembly = File.ReadAllBytes(assemblyPath);
        string source = "// source for " + operationId;
        Dictionary<string, object?> fields = new Dictionary<string, object?>
        {
            ["kind"] = kind, ["protocol"] = 2, ["token"] = token, ["generation"] = generation,
            ["operation_id"] = operationId, ["source"] = source,
            ["source_sha256"] = Hash(Encoding.UTF8.GetBytes(source)), ["assembly_sha256"] = Hash(assembly),
            ["assembly"] = Convert.ToBase64String(assembly),
            ["entry_type"] = kind == "inspect" ? "ValheimDevInspection" : "ValheimDevChange",
            ["evidence_events"] = selectors, ["evidence_timeout_ms"] = timeoutMs
        };
        if (!string.IsNullOrEmpty(changeId))
        {
            fields["change_id"] = changeId;
            fields["expected_operation_id"] = expectedOperationIdOverride ?? ActiveOperationId(changeId);
        }
        return JsonSerializer.Serialize(fields);
    }

    private static string? ActiveOperationId(string changeId)
    {
        foreach (JsonElement change in Status().GetProperty("active_changes").EnumerateArray())
        {
            if (change.GetProperty("change_id").GetString() == changeId)
            {
                return change.GetProperty("operation_id").GetString();
            }
        }
        return null;
    }

    private static Task<string> SendAsync(string json)
    {
        int targetPort = port;
        return Task.Run(() =>
        {
            using TcpClient client = new TcpClient();
            client.Connect("127.0.0.1", targetPort);
            using NetworkStream stream = client.GetStream();
            byte[] request = Encoding.UTF8.GetBytes(json + "\n");
            stream.Write(request, 0, request.Length);
            using StreamReader reader = new StreamReader(stream, new UTF8Encoding(false));
            return reader.ReadLine() ?? throw new IOException("missing runtime response");
        });
    }

    private static string Pump(Task<string> task)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            ValheimDevRuntime.Update();
            Thread.Sleep(1);
        }
        if (!task.IsCompleted) throw new TimeoutException("runtime response did not complete");
        return task.GetAwaiter().GetResult();
    }

    private static void WaitForQueue()
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (ValheimDevRuntime.QueueCountForTests == 0 && DateTime.UtcNow < deadline) Thread.Sleep(1);
        Require(ValheimDevRuntime.QueueCountForTests > 0, "request reached bounded runtime queue");
    }

    private static void Authorize()
    {
        Terminal terminal = new Terminal();
        Require(ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "on" }, terminal), "lab command is routed");
        Require(ValheimDevRuntime.IsAuthorizedForTests, "eligible console command authorizes");
        using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllText(ValheimDevRuntime.DescriptorPath));
        JsonElement value = descriptor.RootElement;
        Require(value.GetProperty("protocol").GetInt32() == 2
            && value.GetProperty("host").GetString() == "127.0.0.1"
            && value.GetProperty("compiler_references").GetArrayLength() == 10,
            "descriptor contains protocol, loopback endpoint, and curated references");
        token = value.GetProperty("token").GetString()!;
        generation = value.GetProperty("generation").GetString()!;
        port = value.GetProperty("port").GetInt32();
    }

    private static ValheimDevBuildIdentity TestIdentity()
    {
        ValheimDevBuildIdentity value = new ValheimDevBuildIdentity
        {
            ValheimVersion = "0.221.12", ValheimSha256 = new string('a', 64),
            BenheimVersion = "test-benheim", BenheimSha256 = new string('b', 64)
        };
        for (int index = 0; index < 10; index++) value.CompilerReferences.Add(Path.Combine(root, "reference-" + index + ".dll"));
        return value;
    }

    private static ValheimDevWorldState EligibleState()
    {
        return new ValheimDevWorldState
        {
            Network = new object(), Scene = new object(), WorldId = 42, IsServer = true,
            LocalPlayer = new object(), LocalPlayerIsAlive = true, LocalPlayerIsOwner = true,
            GameplayHooksHealthy = true
        };
    }

    private static ValheimDevWorldState Clone(ValheimDevWorldState value)
    {
        return new ValheimDevWorldState
        {
            Network = value.Network, Scene = value.Scene, WorldId = value.WorldId,
            IsServer = value.IsServer, IsOpenServer = value.IsOpenServer, IsDedicated = value.IsDedicated,
            PeerCount = value.PeerCount, HasServerRpc = value.HasServerRpc, LocalPlayer = value.LocalPlayer,
            LocalPlayerIsAlive = value.LocalPlayerIsAlive, LocalPlayerIsOwner = value.LocalPlayerIsOwner,
            GameplayHooksHealthy = value.GameplayHooksHealthy
        };
    }

    private static void EachGate(ValheimDevWorldState baseline, Action<ValheimDevWorldState> mutate, string expected)
    {
        ValheimDevWorldState value = Clone(baseline);
        mutate(value);
        Require(ValheimDevEligibility.CheckAuthorization(value) == expected, expected + " gate");
    }

    private static void EachSessionGate(
        ValheimDevWorldCapture capture,
        ValheimDevWorldState baseline,
        Action<ValheimDevWorldState> mutate,
        string expected)
    {
        ValheimDevWorldState value = Clone(baseline);
        mutate(value);
        Require(ValheimDevEligibility.CheckCapturedSession(capture, value) == expected, expected + " captured gate");
    }

    private static JsonElement Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
    }
}
