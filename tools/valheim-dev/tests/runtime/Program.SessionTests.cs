using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Threading;
using ValheimDev;

internal static partial class Program
{
    private static void FailedStartupLeavesNoSession()
    {
        CompilerReferencesFollowTheLoadedRuntime();
        string parent = Path.Combine(Path.GetTempPath(), "valheim-dev-blocked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        root = Path.Combine(parent, "runtime-root");
        File.WriteAllText(root, "blocks the data root");
        ValheimDevRuntime.SetTestHooks(() => state, TestIdentity);
        ValheimDevRuntime.Initialize(root, Path.Combine(root, "LogOutput.log"), "test-valheim-dev", Thread.CurrentThread.ManagedThreadId);

        int previousWarnings = Plugin.Log.Warnings.Count;
        ValheimDevRuntime.Update();
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && Plugin.Log.Warnings.Count == previousWarnings + 1,
            "failed automatic startup remains unauthorized and reports failure once per world");
        Terminal terminal = new Terminal();
        Require(ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "on" }, terminal),
            "failed Lab startup command is routed");
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && terminal.Lines.Count == 1
            && terminal.Lines[0].Contains("session_start_failed", StringComparison.Ordinal),
            "failed Lab startup publishes no authorization or owned session");

        File.Delete(root);
        Directory.Delete(parent);
        root = string.Empty;
    }

    private static void CompilerReferencesFollowTheLoadedRuntime()
    {
        Assembly fileBackedAssembly = typeof(Program).Assembly;
        Assembly dynamicAssembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("ValheimDevDynamicReferenceProof"),
            AssemblyBuilderAccess.Run);
        string[] references = ValheimDevRuntime.ReferenceableAssemblyLocations(new[]
        {
            fileBackedAssembly,
            fileBackedAssembly,
            dynamicAssembly
        });
        string expected = Path.GetFullPath(fileBackedAssembly.Location);
        Require(references.Length == 1
            && references[0] == expected
            && references.SequenceEqual(references.OrderBy(path => path, StringComparer.Ordinal)),
            "compiler references include every unique file-backed loaded assembly and skip dynamic assemblies");
    }

    private static void AcceptedSocketCannotCrossSessions()
    {
        string acceptedSessionId = sessionId;
        using TcpClient staleClient = new TcpClient();
        staleClient.Connect("127.0.0.1", port);
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);
        while (ValheimDevRuntime.ActiveConnectionCountForTests == 0 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(1);
        }
        Require(ValheimDevRuntime.ActiveConnectionCountForTests == 1,
            "replacement-session proof reaches an accepted old-session socket");

        ValheimDevRuntime.Revoke("socket_session_replacement");
        Authorize();
        Require(sessionId != acceptedSessionId, "replacement-session proof rotates the session identity");

        staleClient.ReceiveTimeout = 5000;
        using NetworkStream stream = staleClient.GetStream();
        byte[] request = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["kind"] = "status", ["protocol"] = ValheimDevProtocol.ProtocolVersion, ["session_id"] = sessionId
        }) + "\n");
        stream.Write(request, 0, request.Length);
        using StreamReader reader = new StreamReader(stream, new UTF8Encoding(false));
        JsonElement response = Parse(reader.ReadLine() ?? throw new IOException("missing stale-socket response"));
        Require(response.GetProperty("error").GetString() == "not_authorized"
            && ValheimDevRuntime.QueueCountForTests == 0,
            "a socket accepted by an old Lab session cannot enqueue work for its replacement");
    }

    private static void OffWorldTransitionDoesNotCarryInstalledCode(string changeAssembly)
    {
        ResetRuntime();
        Authorize();
        Environment.SetEnvironmentVariable("VALHEIM_DEV_VARIANT", "old-world");
        Require(Install("old-world-change", "affinity.weapon-icon", changeAssembly)
                .GetProperty("ok").GetBoolean(),
            "world transition proof installs code in the first world");
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, new Terminal());
        Require(ValheimDevTestSurface.Visible, "turning access off leaves first-world code installed");
        state = new ValheimDevWorldState();
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && ValheimDevRuntime.IsCancellationRequested
            && !ValheimDevTestSurface.Visible,
            "world exit ends tracking and cleans installed code even while Lab access is off");
        state = EligibleState();
        ValheimDevRuntime.Update();
        ReadAuthorizedSession();
        Require(Status().GetProperty("active_changes").GetArrayLength() == 0,
            "the next world automatically opens with no installed code from the departed world");

        Require(Install("next-world-change", "affinity.weapon-icon", changeAssembly)
                .GetProperty("ok").GetBoolean(),
            "world replacement proof installs code in the next world");
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, new Terminal());
        state.Scene = new object();
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && !ValheimDevTestSurface.Visible,
            "direct world replacement cleans old installed code before opening another session");
        ValheimDevRuntime.Update();
        ReadAuthorizedSession();
        Require(Status().GetProperty("active_changes").GetArrayLength() == 0
            && !ValheimDevTestSurface.Visible,
            "direct world replacement restores automatic access with an empty registry");
    }

    private static void AutomaticLocalAccess(string changeAssembly)
    {
        var restrictedStates = new (string Name, Action<ValheimDevWorldState> Restrict)[]
        {
            ("no network", value => value.Network = null),
            ("no scene", value => value.Scene = null),
            ("remote client", value => value.IsServer = false),
            ("open server", value => value.IsOpenServer = true),
            ("dedicated server", value => value.IsDedicated = true),
            ("connected peer", value => value.PeerCount = 1),
            ("server RPC", value => value.HasServerRpc = true),
            ("missing player", value => value.LocalPlayer = null),
            ("destroyed player", value => value.LocalPlayerIsAlive = false),
            ("unowned player", value => value.LocalPlayerIsOwner = false)
        };
        foreach (var restricted in restrictedStates)
        {
            ResetRuntime();
            ValheimDevWorldState eligible = Clone(state);
            restricted.Restrict(state);
            ValheimDevRuntime.Update();
            Require(!ValheimDevRuntime.IsAuthorizedForTests
                && !File.Exists(ValheimDevRuntime.DescriptorPath),
                "automatic access rejects " + restricted.Name);
            state = eligible;
            ValheimDevRuntime.Update();
            ReadAuthorizedSession();
            Require(Status().GetProperty("authorized").GetBoolean(),
                "automatic access starts after " + restricted.Name + " becomes eligible");
        }

        string automaticSessionId = sessionId;
        state.LocalPlayer = null;
        ValheimDevRuntime.Update();
        state.LocalPlayer = new object();
        ValheimDevRuntime.Update();
        ReadAuthorizedSession();
        Require(sessionId == automaticSessionId,
            "automatic access retains the world session through player respawn");
        Require(Install("automatic-world-change", "affinity.weapon-icon", changeAssembly)
                .GetProperty("ok").GetBoolean(),
            "automatic authorization supports installed-code execution");
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, new Terminal());
        ValheimDevRuntime.Update();
        state.LocalPlayer = null;
        ValheimDevRuntime.Update();
        state.LocalPlayer = new object();
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && !File.Exists(ValheimDevRuntime.DescriptorPath)
            && !ValheimDevRuntime.IsCancellationRequested
            && ValheimDevTestSurface.Visible,
            "explicit off stays off through respawn and keeps installed code in the same world");
        Authorize();
        Require(sessionId != automaticSessionId
            && Status().GetProperty("active_changes").GetArrayLength() == 1,
            "explicit on reopens an automatically authorized world with a fresh session and its installed code");
        JsonElement staleSession = Parse(Pump(SendAsync(JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["kind"] = "status", ["protocol"] = ValheimDevProtocol.ProtocolVersion,
                ["session_id"] = automaticSessionId
            }))));
        Require(staleSession.GetProperty("error").GetString() == "authorization_mismatch",
            "reopening default access rejects requests prepared for its earlier session");

        ResetRuntime();
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "off" }, new Terminal());
        ValheimDevRuntime.Update();
        state.PeerCount = 1;
        ValheimDevRuntime.Update();
        state.PeerCount = 0;
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests,
            "off before the first automatic attempt survives eligibility changes in that world");
        state.LocalPlayer = null;
        ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "on" }, new Terminal());
        ValheimDevRuntime.Update();
        Require(!ValheimDevRuntime.IsAuthorizedForTests,
            "explicit on still waits for an eligible player");
        state.LocalPlayer = new object();
        ValheimDevRuntime.Update();
        ReadAuthorizedSession();
        Require(ValheimDevRuntime.HasAccess && ValheimDevRuntime.LabSessionId == sessionId,
            "explicit on restores automatic access once the same world becomes eligible");
    }

    private static void RequireLabDiagnostics(params string[] expectedNames)
    {
        int matched = 0;
        foreach (ValheimDevEvidenceRecord diagnosticEvent in ValheimDevDiagnostics.EmittedForTests)
        {
            if (diagnosticEvent.Domain != "ValheimDev") continue;
            Require(matched < expectedNames.Length && diagnosticEvent.Name == expectedNames[matched],
                "Valheim Dev lifecycle diagnostic order remains explicit");
            using JsonDocument document = JsonDocument.Parse(diagnosticEvent.Json);
            Require(document.RootElement.GetProperty("lab_session_id").GetString() == sessionId,
                diagnosticEvent.Name + " carries the current Lab session identity");
            matched++;
        }
        Require(matched == expectedNames.Length, "every expected Valheim Dev lifecycle diagnostic was emitted");
    }

    private static void Authorize()
    {
        Terminal terminal = new Terminal();
        Require(ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "on" }, terminal), "lab command is routed");
        Require(
            ValheimDevRuntime.IsAuthorizedForTests && File.Exists(ValheimDevRuntime.DescriptorPath),
            "eligible console command publishes a usable Lab session: " + string.Join(" | ", terminal.Lines));
        ReadAuthorizedSession();
    }

    private static void ReadAuthorizedSession()
    {
        Require(ValheimDevRuntime.IsAuthorizedForTests && File.Exists(ValheimDevRuntime.DescriptorPath),
            "an eligible world publishes a usable Lab session");
        using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllText(ValheimDevRuntime.DescriptorPath));
        JsonElement value = descriptor.RootElement;
        Require(value.GetProperty("protocol").GetInt32() == ValheimDevProtocol.ProtocolVersion
            && value.GetProperty("host").GetString() == "127.0.0.1"
            && value.GetProperty("compiler_references").GetArrayLength() == 10,
            "descriptor contains protocol, loopback endpoint, and session compiler references");
        sessionId = value.GetProperty("session_id").GetString()!;
        port = value.GetProperty("port").GetInt32();
        using JsonDocument diagnostic = JsonDocument.Parse(
            ValheimDevDiagnostics.LastEmittedForTests?.Json
                ?? throw new InvalidOperationException("Lab authorization diagnostic was not emitted"));
        Require(diagnostic.RootElement.GetProperty("lab_session_id").GetString() == sessionId,
            "Lab diagnostics carry the current session identity");
    }

    private static ValheimDevSessionIdentity TestIdentity()
    {
        ValheimDevSessionIdentity value = new ValheimDevSessionIdentity
        {
            ValheimVersion = "0.221.12", ValheimSha256 = new string('a', 64),
            ValheimDevVersion = "test-valheim-dev", ValheimDevSha256 = new string('b', 64)
        };
        for (int index = 0; index < 10; index++)
        {
            value.CompilerReferences.Add(Path.Combine(root, "reference-" + index + ".dll"));
        }
        return value;
    }
}
