using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using BenheimQoL.Infrastructure;
using BenheimQoL.ValheimDev;

internal static partial class Program
{
    private static void FailedStartupLeavesNoSession()
    {
        root = Path.Combine(Path.GetTempPath(), "benheim-valheim-dev-blocked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "ValheimDev"), "blocks the session directory");
        ValheimDevRuntime.SetTestHooks(() => state, TestIdentity);
        ValheimDevRuntime.Initialize(root, "test-benheim", Thread.CurrentThread.ManagedThreadId);

        Terminal terminal = new Terminal();
        Require(ValheimDevRuntime.TryHandleConsole(new[] { "bh", "lab", "on" }, terminal),
            "failed Lab startup command is routed");
        Require(!ValheimDevRuntime.IsAuthorizedForTests
            && terminal.Lines.Count == 1
            && terminal.Lines[0].Contains("session_start_failed", StringComparison.Ordinal),
            "failed Lab startup publishes no authorization or owned session");

        File.Delete(Path.Combine(root, "ValheimDev"));
        Directory.Delete(root);
        root = string.Empty;
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
            ["kind"] = "status", ["protocol"] = 3, ["session_id"] = sessionId
        }) + "\n");
        stream.Write(request, 0, request.Length);
        using StreamReader reader = new StreamReader(stream, new UTF8Encoding(false));
        JsonElement response = Parse(reader.ReadLine() ?? throw new IOException("missing stale-socket response"));
        Require(response.GetProperty("error").GetString() == "not_authorized"
            && ValheimDevRuntime.QueueCountForTests == 0,
            "a socket accepted by an old Lab session cannot enqueue work for its replacement");
    }

    private static void RequireLabDiagnostics(params string[] expectedNames)
    {
        int matched = 0;
        foreach (DiagnosticEvent diagnosticEvent in Diagnostics.EmittedForTests)
        {
            if (diagnosticEvent.Domain != "ValheimDev") continue;
            Require(matched < expectedNames.Length && diagnosticEvent.Name == expectedNames[matched],
                "Valheim Dev lifecycle diagnostic order remains explicit");
            using JsonDocument document = JsonDocument.Parse(diagnosticEvent.ToJsonLine());
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
        using JsonDocument descriptor = JsonDocument.Parse(File.ReadAllText(ValheimDevRuntime.DescriptorPath));
        JsonElement value = descriptor.RootElement;
        Require(value.GetProperty("protocol").GetInt32() == 3
            && value.GetProperty("host").GetString() == "127.0.0.1"
            && value.GetProperty("compiler_references").GetArrayLength() == 10,
            "descriptor contains protocol, loopback endpoint, and curated references");
        sessionId = value.GetProperty("session_id").GetString()!;
        port = value.GetProperty("port").GetInt32();
        using JsonDocument diagnostic = JsonDocument.Parse(
            Diagnostics.LastEmittedForTests?.ToJsonLine()
                ?? throw new InvalidOperationException("Lab authorization diagnostic was not emitted"));
        Require(diagnostic.RootElement.GetProperty("session").GetString() == "test-diagnostic-session"
            && diagnostic.RootElement.GetProperty("lab_session_id").GetString() == sessionId,
            "diagnostic envelope and Lab session identities remain distinct");
    }

    private static ValheimDevSessionIdentity TestIdentity()
    {
        ValheimDevSessionIdentity value = new ValheimDevSessionIdentity
        {
            ValheimVersion = "0.221.12", ValheimSha256 = new string('a', 64),
            BenheimVersion = "test-benheim", BenheimSha256 = new string('b', 64)
        };
        for (int index = 0; index < 10; index++)
        {
            value.CompilerReferences.Add(Path.Combine(root, "reference-" + index + ".dll"));
        }
        return value;
    }
}
