using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ValheimDev;

internal static class Program
{
    private static readonly string AppId = Guid.NewGuid().ToString("D");
    private static int assertions;
    private static void Main()
    {
        ThreadPool.SetMinThreads(32, 32);
        Protocol(); Ownership(); Transport();
        Console.WriteLine("Valheim Dev app runtime checks passed (" + assertions + " assertions).");
    }

    private static void Protocol()
    {
        Check(Parse("status", ""), "status parses");
        Check(Parse("create_world", ",\"name\":\"Lab-proof\",\"seed\":\"abc123\""), "world parses");
        Check(!Parse("create_world", ",\"name\":\"normal\""), "normal name refused");
        Check(!Parse("create_world", ",\"name\":\"Lab-../x\""), "path refused");
        Check(!Parse("create_character", ",\"name\":\"Lab-x\",\"seed\":\"x\""), "extra fields refused");
        Check(!Parse("status", ",\"source\":\"code\""), "code cannot enter app bridge");
        Check(!Parse("run_once", ""), "execution action refused");
        Check(!Parse("open_lab", ",\"world\":\"Lab-x\""), "both saves required");
        Check(!Parse("status", ",\"action\":\"status\""), "duplicate fields refused");
        Check(!ValheimDevAppProtocol.TryParse(new string('x', 16385), out _, out string error) && error == "request_too_large", "request bounded");
        Check(!ValheimDevAppProtocol.IsDisposableName("Lab-"), "empty suffix refused");
        Check(ValheimDevAppProtocol.IsDisposableName("Lab-a_B-3"), "ascii Lab names allowed");
        Check(!ValheimDevAppProtocol.IsDisposableName("Lab-" + new string('a', 45)), "name length bounded");
        Check(ValheimDevAppProtocol.Response(AppId, "", null, "{\"big\":\"" + new string('a', 270000) + "\"}").Contains("response_too_large"), "response bounded");
    }

    private static bool Parse(string action, string fields) => ValheimDevAppProtocol.TryParse(Wire(AppId, action, fields), out _, out _);
    private static string Wire(string appId, string action, string fields = "") =>
        "{\"protocol\":1,\"app_id\":\"" + appId + "\",\"request_id\":\"" + Guid.NewGuid() + "\",\"action\":\"" + action + "\"" + fields + "}";
    private static ValheimDevAppRequest Request(string action, string name = "", string world = "", string character = "") =>
        new ValheimDevAppRequest { AppId = AppId, RequestId = Guid.NewGuid().ToString(), Action = action, Name = name, World = world, Character = character };
    private static JsonElement Result(ValheimDevAppController controller, string action = "status") => JsonDocument.Parse(controller.Process(Request(action))).RootElement.GetProperty("result");
    private static bool Ok(string response) => JsonDocument.Parse(response).RootElement.GetProperty("ok").GetBoolean();

    private static void Ownership()
    {
        Native native = new Native();
        ValheimDevAppController controller = new ValheimDevAppController(native, AppId, 42, "");
        Check(!Result(controller).GetProperty("owned").GetBoolean(), "ordinary menu unowned");
        Check(!Ok(controller.Process(Request("close_lab"))), "ordinary menu cannot quit");
        Check(Ok(controller.Process(Request("create_world", "Lab-world"))), "local world creation confirmed");
        Check(!Ok(controller.Process(Request("create_world", "Lab-world"))), "world collision refused");
        Check(Ok(controller.Process(Request("create_character", "Lab-player"))), "local character creation confirmed");
        native.WorldSaves.Add(Native.Save("Lab-cloud", 200, "cloud"));
        Check(!Ok(controller.Process(Request("create_world", "Lab-cloud"))), "cloud collision refused");
        Check(!Ok(controller.Process(Request("open_lab", world: "Lab-cloud", character: "Lab-player"))), "cloud cannot open");
        native.FailSave = true;
        Check(!Ok(controller.Process(Request("create_world", "Lab-failure"))), "silent native save failure detected");
        native.FailSave = false;
        Check(Ok(controller.Process(Request("open_lab", world: "Lab-world", character: "Lab-player"))), "native open accepted");
        Check(!Result(controller).GetProperty("owned").GetBoolean(), "open request does not prove ownership");
        native.EnterWorld(native.WorldSaves[0], native.CharacterSaves[0]);
        Check(Result(controller).GetProperty("owned").GetBoolean(), "exact native IDs establish ownership");
        native.State.LabAccess = false;
        Check(Result(controller).GetProperty("owned").GetBoolean(), "lab off retains owned world");
        Check(!Ok(controller.Process(Request("create_character", "Lab-nope"))), "creation denied in world");
        ValheimDevAppRequest close = Request("close_lab");
        Check(Ok(controller.Process(close)), "owned world logout accepted");
        Check(native.Calls.Contains("logout") && !native.Calls.Contains("quit"), "quit waits for save and menu");
        native.State.State = "closing";
        controller.Observe();
        Check(!native.Calls.Contains("quit"), "still no quit while shutdown incomplete");
        native.State = new ValheimDevAppState { State = "menu" };
        controller.Observe();
        Check(native.Calls.Count(call => call == "quit") == 1, "quit after observed menu");

        Native drift = new Native();
        drift.WorldSaves.Add(Native.Save("Lab-world", 11)); drift.CharacterSaves.Add(Native.Save("Lab-player", 12));
        ValheimDevAppController driftController = new ValheimDevAppController(drift, AppId, 42, "launch");
        Check(Result(driftController).GetProperty("owned").GetBoolean(), "tools launch owns initial menu");
        Check(Ok(driftController.Process(Request("open_lab", world: "Lab-world", character: "Lab-player"))), "open for drift test");
        drift.EnterWorld(drift.WorldSaves[0], Native.Save("Lab-player", 99));
        Check(!Result(driftController).GetProperty("owned").GetBoolean(), "changed character ID never owned");
        Check(!Ok(driftController.Process(Request("close_lab"))), "changed save cannot quit");
        drift.State = new ValheimDevAppState { State = "menu" };
        Check(!Ok(driftController.Process(Request("close_lab"))), "launch token cannot re-own menu after foreign world");

        Native reconnect = new Native();
        reconnect.WorldSaves.Add(Native.Save("Lab-world", 11)); reconnect.CharacterSaves.Add(Native.Save("Lab-player", 12));
        ValheimDevAppController reconnectController = new ValheimDevAppController(reconnect, AppId, 42, "");
        reconnectController.Process(Request("open_lab", world: "Lab-world", character: "Lab-player"));
        reconnect.EnterWorld(reconnect.WorldSaves[0], reconnect.CharacterSaves[0]);
        Check(Result(reconnectController).GetProperty("owned").GetBoolean(), "second ownership established");
        reconnect.State.Network = new object();
        Check(!Ok(reconnectController.Process(Request("close_lab"))), "same saves in new network session not owned");
        reconnect.State.State = "multiplayer"; reconnect.State.EligibleLocal = false;
        Check(!Ok(reconnectController.Process(Request("close_lab"))), "multiplayer cannot quit");

        Native delayed = new Native();
        delayed.WorldSaves.Add(Native.Save("Lab-world", 11)); delayed.CharacterSaves.Add(Native.Save("Lab-player", 12));
        ValheimDevAppController delayedController = new ValheimDevAppController(delayed, AppId, 42, "");
        delayedController.Process(Request("open_lab", world: "Lab-world", character: "Lab-player"));
        delayed.EnterWorld(delayed.WorldSaves[0], delayed.CharacterSaves[0]);
        delayedController.Observe();
        Check(Ok(delayedController.Process(Request("close_lab"))), "delayed native logout accepted");
        delayed.EnterWorld(delayed.WorldSaves[0], delayed.CharacterSaves[0]);
        delayedController.Observe(); // New network instance means another session, even with identical saves.
        delayed.State = new ValheimDevAppState { State = "menu" };
        delayedController.Observe();
        Check(!delayed.Calls.Contains("quit"), "stale close never quits menu reached from another local world");
        Check(ValheimDevDiagnostics.EmittedForTests.Any(record => record.Name == "app_operation_result" && record.Json.Contains("session_not_owned")), "rejections have structured evidence");
    }

    private static void Transport()
    {
        string root = Path.Combine(Path.GetTempPath(), "valheim-app-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            Native native = new Native();
            ValheimDevSessionIdentity identity = new ValheimDevSessionIdentity
            { ValheimVersion = "exact", ValheimSha256 = new string('a', 64), ValheimDevVersion = "0.5.0", ValheimDevSha256 = new string('b', 64) };
            using ValheimDevAppRuntime runtime = new ValheimDevAppRuntime(root, Thread.CurrentThread.ManagedThreadId, native, identity, "launch-id");
            JsonElement descriptor = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "app.json"))).RootElement;
            Check(descriptor.GetProperty("host").GetString() == "127.0.0.1", "loopback descriptor");
            Check(descriptor.GetProperty("app_id").GetString() == runtime.AppId && descriptor.GetProperty("process_started_utc").GetString()!.Length > 0, "fresh process identity");
            Check(runtime.AppId.Length == 32 && runtime.AppId.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'), "app identity matches Node lowercase hex contract");
            Check(descriptor.GetProperty("valheim_dev_sha256").GetString() == identity.ValheimDevSha256, "exact build published");
            int port = descriptor.GetProperty("port").GetInt32();
            Task<string> stale = Send(port, Wire(AppId, "create_world", ",\"name\":\"Lab-stale\""));
            Check(stale.Wait(3000) && stale.Result.Contains("stale_app"), "stale process refused off-thread");
            Check(native.Calls.Count == 0, "stale request never touches native");
            Task<string> valid = Send(port, Wire(runtime.AppId, "create_world", ",\"name\":\"Lab-transport\""));
            Check(SpinWait.SpinUntil(() => runtime.QueueCountForTests == 1, 3000), "socket queues request");
            Check(native.Calls.Count == 0 && !valid.IsCompleted, "native waits for main Update");
            Task.Run(runtime.Update).GetAwaiter().GetResult();
            Check(runtime.QueueCountForTests == 1 && native.Calls.Count == 0, "wrong-thread Update cannot execute");
            runtime.Update();
            Check(valid.Wait(3000) && Ok(valid.Result), "main thread returns structured result");
            Check(native.WorldSaves.Count == 1, "native action ran once");

            List<Task<string>> queued = Enumerable.Range(0, 9).Select(_ => Send(port, Wire(runtime.AppId, "status"))).ToList();
            Check(SpinWait.SpinUntil(() => runtime.QueueCountForTests == 8 && queued.Any(task => task.IsCompleted), 3000), "queue bounded at eight");
            Check(queued.Any(task => task.IsCompletedSuccessfully && task.Result.Contains("queue_full")), "excess request explicitly refused");
            runtime.Dispose();
            Check(Task.WaitAll(queued.ToArray(), 3000), "teardown unblocks queued work");
            Check(!File.Exists(Path.Combine(root, "app.json")), "teardown deletes own descriptor");
            using ValheimDevAppRuntime replacement = new ValheimDevAppRuntime(root, Thread.CurrentThread.ManagedThreadId, native, identity, "");
            Check(replacement.AppId != runtime.AppId, "next bridge gets fresh identity");
            string replacementDescriptor = File.ReadAllText(Path.Combine(root, "app.json"));
            runtime.Dispose();
            Check(File.ReadAllText(Path.Combine(root, "app.json")) == replacementDescriptor, "old teardown preserves new descriptor");
        }
        finally { Directory.Delete(root, true); }
    }

    private static Task<string> Send(int port, string wire) => Task.Run(() =>
    {
        using TcpClient client = new TcpClient("127.0.0.1", port);
        client.ReceiveTimeout = 5000;
        NetworkStream stream = client.GetStream();
        byte[] bytes = Encoding.UTF8.GetBytes(wire + "\n"); stream.Write(bytes, 0, bytes.Length);
        return new StreamReader(stream).ReadLine() ?? throw new Exception("no_response");
    });

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Assertion failed: " + message);
    }
}
