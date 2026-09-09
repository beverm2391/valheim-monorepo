using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ValheimDev;

internal static partial class Program
{
    private static ValheimDevWorldState state = EligibleState();
    private static string sessionId = string.Empty;
    private static int port;
    private static string root = string.Empty;

    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 5) throw new InvalidOperationException("five fixture assemblies are required");
            ValheimDevDiagnostics.SetEvidenceSubscriptionForTests(_ => "available");
            ConsoleComposition();
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

    private static void ConsoleComposition()
    {
        Terminal.ClearCommands();
        ValheimDevConsole.Initialize();
        Terminal standaloneTerminal = new Terminal();
        Terminal.Command("bh").RunAction(new Terminal.ConsoleEventArgs("bh help", standaloneTerminal));
        Require(standaloneTerminal.Lines.Count > 0,
            "standalone Lab command remains available without Benheim");
        ValheimDevConsole.Reset();

        Terminal.ClearCommands();
        int priorRuns = 0;
        _ = new Terminal.ConsoleCommand("bh", "fixture", _ => { priorRuns++; return true; }, isNetwork: true);
        ValheimDevConsole.Initialize();
        Terminal terminal = new Terminal();
        Terminal.Command("bh").RunAction(new Terminal.ConsoleEventArgs("bh help", terminal));
        Require(priorRuns == 1, "standalone Lab command composes with Benheim's existing bh command");
        ValheimDevConsole.Reset();
        Terminal.Command("bh").RunAction(new Terminal.ConsoleEventArgs("bh help", terminal));
        Require(priorRuns == 2, "removing Valheim Dev restores Benheim's bh command");
    }
    private static void RuntimeLifecycle(string[] fixtures)
    {
        FailedStartupLeavesNoSession();
        root = Path.Combine(Path.GetTempPath(), "valheim-dev-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "session.json"), "stale");
        ValheimDevRuntime.SetTestHooks(() => state, TestIdentity);
        ResetRuntime();
        Require(!File.Exists(ValheimDevRuntime.DescriptorPath), "startup removes stale descriptor");

        Authorize();
        string firstSessionId = sessionId;
        JsonElement outdatedProtocol = Parse(Pump(SendAsync(JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["kind"] = "status", ["protocol"] = 3, ["session_id"] = sessionId
            }))));
        Require(outdatedProtocol.GetProperty("error").GetString() == "protocol_mismatch",
            "the runtime rejects the superseded version-3 wire protocol explicitly");
        Require(Status().GetProperty("active_changes").GetArrayLength() == 0, "status starts with no managed changes");

        Task<string> wrongThreadInspection = SendAsync(CodeRequest(
            "run_once", "wrong-thread", string.Empty, fixtures[0], Array.Empty<string>(), 0));
        WaitForQueue();
        Thread wrong = new Thread(ValheimDevRuntime.Update);
        wrong.Start();
        wrong.Join();
        Thread.Sleep(25);
        Require(!wrongThreadInspection.IsCompleted, "only Unity's main thread can inspect the live runtime");
        Require(Parse(Pump(wrongThreadInspection)).GetProperty("ok").GetBoolean(),
            "main-thread Update completes the queued inspection");

        ValheimDevTestSurface.Reset();
        JsonElement inspection = RunOnce(
            "inspect-affinity-icon",
            fixtures[0],
            "{\"selector\":\"inventory\"}");
        JsonElement inspectionResult = Parse(inspection.GetProperty("result").GetString()!);
        Require(inspection.GetProperty("ok").GetBoolean()
            && inspection.GetProperty("result").GetString()!.Contains("Affinity.weapon_icon", StringComparison.Ordinal)
            && inspectionResult.GetProperty("input").GetProperty("selector").GetString() == "inventory"
            && inspection.GetProperty("cleanup_state").GetString() == "not_applicable"
            && !ValheimDevTestSurface.Visible,
            "inspection describes the live icon surface without installing a change");

        Environment.SetEnvironmentVariable("VALHEIM_DEV_COMMAND_VARIANT", "one-shot");
        JsonElement command = RunOnce("change-value-once", fixtures[0]);
        Require(command.GetProperty("ok").GetBoolean()
            && command.GetProperty("cleanup_state").GetString() == "not_applicable"
            && ValheimDevTestSurface.Visible && ValheimDevTestSurface.Variant == "one-shot",
            "run_once can change live state without claiming cleanup or installing code");
        Environment.SetEnvironmentVariable("VALHEIM_DEV_COMMAND_VARIANT", null);
        ValheimDevTestSurface.Reset();

        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "pulse-a");
        Task<string> selectedTask = SendAsync(CodeRequest(
            "install_change", "install-a", "affinity.weapon-icon", fixtures[0],
            new[] { "Affinity:weapon_icon_observed" }, 5000));
        WaitForQueue();
        ValheimDevRuntime.Update();
        Require(!selectedTask.IsCompleted && ValheimDevTestSurface.Visible
            && ValheimDevTestSurface.Variant == "pulse-a",
            "managed change remains active while selected evidence is observed");
        ValheimDevDiagnostics.PublishEvidenceForTests(
            "Affinity",
            "weapon_icon_observed",
            "{\"domain\":\"Affinity\",\"event\":\"weapon_icon_observed\",\"variant\":\"pulse-a\"}");
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

        Task<string> queued = SendAsync(CodeRequest("run_once", "queued-before-off", string.Empty, fixtures[0], Array.Empty<string>(), 0));
        WaitForQueue();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, new Terminal());
        JsonElement canceled = Parse(queued.GetAwaiter().GetResult());
        Require(canceled.GetProperty("error").GetString() == "authorization_revoked"
            && !ValheimDevRuntime.IsAuthorizedForTests && !File.Exists(ValheimDevRuntime.DescriptorPath),
            "off invalidates the descriptor and cancels queued work");

        Authorize();
        Require(sessionId != firstSessionId, "reauthorization creates a new session identity");
        JsonElement staleSession = Parse(Pump(SendAsync(JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["kind"] = "status", ["protocol"] = ValheimDevProtocol.ProtocolVersion, ["session_id"] = firstSessionId
            }))));
        Require(staleSession.GetProperty("error").GetString() == "authorization_mismatch",
            "a request from the previous Lab session is rejected");
        AcceptedSocketCannotCrossSessions();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "revoked");
        ValheimDevDiagnostics.ClearEmittedForTests();
        Require(Install("install-before-off", "affinity.weapon-icon", fixtures[0]).GetProperty("ok").GetBoolean(),
            "managed change installs before explicit revocation");
        RequireLabDiagnostics("operation_started", "operation_finished");
        string revokedSessionId = sessionId;
        ValheimDevDiagnostics.ClearEmittedForTests();
        Terminal explicitOff = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, explicitOff);
        Require(ValheimDevTestSurface.Visible
            && !ValheimDevRuntime.IsAuthorizedForTests
            && !ValheimDevRuntime.IsCancellationRequested
            && !File.Exists(ValheimDevRuntime.DescriptorPath)
            && explicitOff.Lines[0].Contains("1 installed change(s) remain active", StringComparison.Ordinal),
            "off closes access without removing installed code from the current world");
        RequireLabDiagnostics("lab_revoked");
        using (JsonDocument revokedDiagnostic = JsonDocument.Parse(
            ValheimDevDiagnostics.LastEmittedForTests?.Json
                ?? throw new InvalidOperationException("Lab revocation diagnostic was not emitted")))
        {
            Require(revokedDiagnostic.RootElement.GetProperty("lab_session_id").GetString() == revokedSessionId,
                "revocation remains correlated to the Lab session it ended");
        }
        Authorize();
        Require(Status().GetProperty("active_changes").GetArrayLength() == 1
            && ValheimDevTestSurface.Visible,
            "reauthorizing the same world rediscovers its installed code");
        Require(Remove("remove-after-reauthorize", "affinity.weapon-icon")
                .GetProperty("ok").GetBoolean()
            && !ValheimDevTestSurface.Visible,
            "installed code remains explicitly removable after reauthorization");

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
            "off preserves sticky restart reporting without another cleanup attempt");

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
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "world-drift");
        Install("install-before-world-drift", "affinity.weapon-icon", fixtures[0]);
        state.Scene = new object();
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && !File.Exists(ValheimDevRuntime.DescriptorPath)
            && ValheimDevRuntime.IsCancellationRequested
            && !ValheimDevTestSurface.Visible,
            "world identity drift ends tracking and cleans installed code before another world");
        OffWorldTransitionDoesNotCarryInstalledCode(fixtures[0]);
    }

    private static void ResetRuntime()
    {
        ValheimDevRuntime.Revoke("simulated_process_restart");
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_RUN", null);
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_ON_RESTORE", null);
        Environment.SetEnvironmentVariable("VALHEIM_DEV_FAIL_CLEANUP_ONCE", null);
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", null);
        Environment.SetEnvironmentVariable("VALHEIM_DEV_COMMAND_VARIANT", null);
        state = EligibleState();
        ValheimDevTestSurface.Reset();
        ValheimDevRuntime.Initialize(root, Path.Combine(root, "LogOutput.log"), "test-valheim-dev", Thread.CurrentThread.ManagedThreadId);
    }

    private static JsonElement Status()
    {
        return Parse(Pump(SendAsync(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "status", ["protocol"] = ValheimDevProtocol.ProtocolVersion, ["session_id"] = sessionId
        }))));
    }

    private static JsonElement RunOnce(string operationId, string assemblyPath, string inputJson = "{}")
        => Parse(Pump(SendAsync(CodeRequest(
            "run_once", operationId, string.Empty, assemblyPath, Array.Empty<string>(), 0,
            inputJson: inputJson))));

    private static JsonElement Install(string operationId, string changeId, string assemblyPath)
        => Parse(Pump(SendAsync(CodeRequest("install_change", operationId, changeId, assemblyPath, Array.Empty<string>(), 0))));

    private static JsonElement Remove(
        string operationId,
        string changeId,
        string? expectedOperationIdOverride = null)
    {
        return Parse(Pump(SendAsync(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "remove_change", ["protocol"] = ValheimDevProtocol.ProtocolVersion, ["session_id"] = sessionId,
            ["operation_id"] = operationId, ["change_id"] = changeId,
            ["expected_operation_id"] = expectedOperationIdOverride ?? ActiveOperationId(changeId)
        }))));
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

    private static ValheimDevWorldState EligibleState()
    {
        return new ValheimDevWorldState
        {
            Network = new object(), Scene = new object(), WorldId = 42, IsServer = true,
            LocalPlayer = new object(), LocalPlayerIsAlive = true, LocalPlayerIsOwner = true
        };
    }

    private static ValheimDevWorldState Clone(ValheimDevWorldState value)
    {
        return new ValheimDevWorldState
        {
            Network = value.Network, Scene = value.Scene, WorldId = value.WorldId,
            IsServer = value.IsServer, IsOpenServer = value.IsOpenServer, IsDedicated = value.IsDedicated,
            PeerCount = value.PeerCount, HasServerRpc = value.HasServerRpc, LocalPlayer = value.LocalPlayer,
            LocalPlayerIsAlive = value.LocalPlayerIsAlive, LocalPlayerIsOwner = value.LocalPlayerIsOwner
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
