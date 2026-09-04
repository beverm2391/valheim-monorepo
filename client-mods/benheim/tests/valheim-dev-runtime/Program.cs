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
        state = EligibleState();
        ValheimDevRuntime.SetTestHooks(() => state, TestIdentity);
        ValheimDevRuntime.Initialize(root, "test-benheim", Thread.CurrentThread.ManagedThreadId);
        Require(!File.Exists(ValheimDevRuntime.DescriptorPath), "startup removes stale descriptor");

        Authorize();
        string firstToken = token;
        string firstGeneration = generation;
        Require(File.Exists(ValheimDevRuntime.DescriptorPath), "authorization creates descriptor");
        Require(Status().GetProperty("ok").GetBoolean(), "status confirms authorized session");

        Task<string> wrongThreadApply = SendAsync(ApplyRequest("wrong-thread", fixtures[0], Array.Empty<string>(), 0));
        WaitForQueue();
        Thread wrong = new Thread(ValheimDevRuntime.Update);
        wrong.Start();
        wrong.Join();
        Thread.Sleep(25);
        Require(!wrongThreadApply.IsCompleted, "transport thread and wrong-thread Update cannot execute apply");
        JsonElement wrongThreadResult = Parse(Pump(wrongThreadApply));
        Require(wrongThreadResult.GetProperty("ok").GetBoolean()
            && wrongThreadResult.GetProperty("cleanup_state").GetString() == "cleaned",
            "main-thread Update executes and cleans its queued apply before response");

        string cleanupMarker = Path.Combine(root, "cleanup-marker.txt");
        Environment.SetEnvironmentVariable("VALHEIM_DEV_CLEANUP_MARKER", cleanupMarker);
        JsonElement replacement = Apply("replacement", fixtures[0]);
        Require(replacement.GetProperty("ok").GetBoolean()
            && replacement.GetProperty("cleanup_state").GetString() == "cleaned"
            && File.Exists(cleanupMarker),
            "apply completes its own cleanup before returning");

        if (File.Exists(cleanupMarker)) File.Delete(cleanupMarker);
        JsonElement synchronous = Parse(Pump(SendAsync(ApplyRequest(
            "synchronous-zero-timeout",
            fixtures[0],
            new[] { "Test:synchronous_run" },
            0))));
        Require(synchronous.GetProperty("ok").GetBoolean()
            && synchronous.GetProperty("evidence_events").GetArrayLength() == 1
            && synchronous.GetProperty("cleanup_state").GetString() == "cleaned"
            && File.Exists(cleanupMarker),
            "zero-timeout apply drains synchronous evidence and cleans before response");
        File.Delete(cleanupMarker);

        Task<string> selectedTask = SendAsync(ApplyRequest(
            "selected-evidence",
            fixtures[0],
            new[] { "Affinity:lunge_attempt_accepted" },
            5000));
        WaitForQueue();
        ValheimDevRuntime.Update();
        Require(!selectedTask.IsCompleted, "selected evidence waits cooperatively across frames");
        Require(!File.Exists(cleanupMarker), "experiment remains active while its evidence window is open");
        for (int index = 0; index < ValheimDevProtocol.MaximumEvidenceEvents * 3; index++)
        {
            Diagnostics.Emit(DiagnosticEvent.Create("Unrelated", "noise"));
        }
        Diagnostics.Emit(DiagnosticEvent.Create("Affinity", "lunge_attempt_accepted").String("operation_id", "game-event"));
        JsonElement selected = Parse(Pump(selectedTask));
        JsonElement selectedEvents = selected.GetProperty("evidence_events");
        Require(selected.GetProperty("evidence_selected").GetBoolean()
            && !selected.GetProperty("evidence_exhaustive").GetBoolean()
            && selectedEvents.GetArrayLength() == 1
            && selectedEvents[0].GetString()!.Contains("lunge_attempt_accepted", StringComparison.Ordinal),
            "unrelated traffic cannot evict selected typed evidence");
        Require(selected.GetProperty("cleanup_state").GetString() == "cleaned"
            && File.Exists(cleanupMarker),
            "evidence-window experiment cleans before its terminal response");

        state.LocalPlayer = new object();
        state.LocalPlayerIsAlive = true;
        state.LocalPlayerIsOwner = true;
        Require(Apply("after-respawn", fixtures[0]).GetProperty("ok").GetBoolean(), "authorized world survives local Player replacement");

        Task<string> queued = SendAsync(ApplyRequest("queued-before-off", fixtures[0], Array.Empty<string>(), 0));
        WaitForQueue();
        Terminal terminal = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, terminal);
        JsonElement canceled = Parse(queued.GetAwaiter().GetResult());
        Require(canceled.GetProperty("error").GetString() == "authorization_revoked"
            && !ValheimDevRuntime.IsAuthorizedForTests
            && !File.Exists(ValheimDevRuntime.DescriptorPath),
            "off invalidates descriptor and cancels queued work before execution");

        Authorize();
        Require(token != firstToken && generation != firstGeneration, "reauthorization rotates token and generation");
        JsonElement noCleanup = Apply("no-cleanup", fixtures[1]);
        Require(noCleanup.GetProperty("cleanup_state").GetString() == "restart_required", "missing cleanup reports restart_required");
        if (File.Exists(cleanupMarker)) File.Delete(cleanupMarker);
        JsonElement refusedAfterMissing = Apply("refused-after-missing-cleanup", fixtures[0]);
        Require(refusedAfterMissing.GetProperty("error").GetString() == "restart_required"
            && refusedAfterMissing.GetProperty("cleanup_state").GetString() == "not_applicable"
            && !File.Exists(cleanupMarker),
            "sticky restart requirement refuses the next apply before loading it");

        Terminal stickyOff = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, stickyOff);
        Terminal stickyOn = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "on" }, stickyOn);
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && stickyOn.Lines.Exists(line => line.Contains("restart_required", StringComparison.Ordinal)),
            "off/on cannot bypass a process-lifetime restart requirement");

        ValheimDevRuntime.Initialize(root, "test-benheim", Thread.CurrentThread.ManagedThreadId);
        Authorize();
        JsonElement failCleanup = Apply("failing-cleanup", fixtures[4]);
        Require(failCleanup.GetProperty("cleanup_state").GetString() == "restart_required",
            "cleanup failure is reported on the operation that owns cleanup");
        if (File.Exists(cleanupMarker)) File.Delete(cleanupMarker);
        JsonElement afterFailure = Apply("after-failing-cleanup", fixtures[0]);
        Require(afterFailure.GetProperty("error").GetString() == "restart_required"
            && afterFailure.GetProperty("cleanup_state").GetString() == "not_applicable"
            && !File.Exists(cleanupMarker),
            "cleanup failure makes restart sticky for subsequent applies");

        Terminal failureOff = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, failureOff);
        Terminal failureOn = new Terminal();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "on" }, failureOn);
        Require(!ValheimDevRuntime.IsAuthorizedForTests,
            "failed cleanup restart requirement also survives off/on");

        ValheimDevRuntime.Initialize(root, "test-benheim", Thread.CurrentThread.ManagedThreadId);
        Authorize();

        JsonElement exception = Apply("throws", fixtures[2]);
        Require(exception.GetProperty("error").GetString() == "experiment_exception"
            && exception.GetProperty("exception").GetString()!.Contains("experiment exploded", StringComparison.Ordinal),
            "runtime returns experiment exception");
        JsonElement invalid = Apply("bad-entrypoint", fixtures[3]);
        Require(invalid.GetProperty("error").GetString() == "run_entrypoint_invalid", "runtime returns entrypoint error");

        ValheimDevRuntime.Revoke("prepare_world_exit");
        state = EligibleState();
        Authorize();
        state.Scene = new object();
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests && !File.Exists(ValheimDevRuntime.DescriptorPath),
            "world identity drift revokes synchronously on Update");

        state = EligibleState();
        Authorize();
        ValheimDevRuntime.Revoke("plugin_teardown");
        Require(!ValheimDevRuntime.IsAuthorizedForTests && !File.Exists(ValheimDevRuntime.DescriptorPath),
            "plugin teardown revokes and removes descriptor");
    }

    private static JsonElement Status()
    {
        return Parse(Pump(SendAsync(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "status",
            ["protocol"] = 1,
            ["token"] = token,
            ["generation"] = generation
        }))));
    }

    private static JsonElement Apply(string operationId, string assemblyPath)
    {
        return Parse(Pump(SendAsync(ApplyRequest(operationId, assemblyPath, Array.Empty<string>(), 0))));
    }

    private static string ApplyRequest(
        string operationId,
        string assemblyPath,
        string[] selectors,
        int timeoutMs)
    {
        byte[] assembly = File.ReadAllBytes(assemblyPath);
        string source = "// source for " + operationId;
        return JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "apply",
            ["protocol"] = 1,
            ["token"] = token,
            ["generation"] = generation,
            ["operation_id"] = operationId,
            ["source"] = source,
            ["source_sha256"] = Hash(Encoding.UTF8.GetBytes(source)),
            ["assembly_sha256"] = Hash(assembly),
            ["assembly"] = Convert.ToBase64String(assembly),
            ["entry_type"] = "ValheimDevExperiment",
            ["evidence_events"] = selectors,
            ["evidence_timeout_ms"] = timeoutMs
        });
    }

    private static string MinimalApplyProtocolRequest(string timeoutJson)
    {
        return "{\"kind\":\"apply\",\"protocol\":1,\"token\":\"t\",\"generation\":\"g\","
            + "\"operation_id\":\"op\",\"source\":\"x\",\"source_sha256\":\"" + new string('0', 64) + "\","
            + "\"assembly_sha256\":\"" + new string('0', 64) + "\",\"assembly\":\"AA==\","
            + "\"entry_type\":\"ValheimDevExperiment\",\"evidence_events\":[],\"evidence_timeout_ms\":"
            + timeoutJson + "}";
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
        JsonElement rootElement = descriptor.RootElement;
        Require(rootElement.GetProperty("protocol").GetInt32() == 1
            && rootElement.GetProperty("host").GetString() == "127.0.0.1"
            && rootElement.GetProperty("compiler_references").GetArrayLength() == 10,
            "descriptor contains protocol, loopback endpoint, and curated references");
        token = rootElement.GetProperty("token").GetString()!;
        generation = rootElement.GetProperty("generation").GetString()!;
        port = rootElement.GetProperty("port").GetInt32();
    }

    private static ValheimDevBuildIdentity TestIdentity()
    {
        ValheimDevBuildIdentity value = new ValheimDevBuildIdentity
        {
            ValheimVersion = "0.221.12",
            ValheimSha256 = new string('a', 64),
            BenheimVersion = "test-benheim",
            BenheimSha256 = new string('b', 64)
        };
        for (int index = 0; index < 10; index++) value.CompilerReferences.Add(Path.Combine(root, "reference-" + index + ".dll"));
        return value;
    }

    private static ValheimDevWorldState EligibleState()
    {
        return new ValheimDevWorldState
        {
            Network = new object(),
            Scene = new object(),
            WorldId = 42,
            IsServer = true,
            IsOpenServer = false,
            IsDedicated = false,
            PeerCount = 0,
            HasServerRpc = false,
            LocalPlayer = new object(),
            LocalPlayerIsAlive = true,
            LocalPlayerIsOwner = true,
            GameplayHooksHealthy = true
        };
    }

    private static ValheimDevWorldState Clone(ValheimDevWorldState value)
    {
        return new ValheimDevWorldState
        {
            Network = value.Network,
            Scene = value.Scene,
            WorldId = value.WorldId,
            IsServer = value.IsServer,
            IsOpenServer = value.IsOpenServer,
            IsDedicated = value.IsDedicated,
            PeerCount = value.PeerCount,
            HasServerRpc = value.HasServerRpc,
            LocalPlayer = value.LocalPlayer,
            LocalPlayerIsAlive = value.LocalPlayerIsAlive,
            LocalPlayerIsOwner = value.LocalPlayerIsOwner,
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

    private static string Hash(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
    }
}
