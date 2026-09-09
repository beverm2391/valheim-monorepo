using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx.Logging;
using BenheimQoL.Infrastructure;

string testRoot = Path.Combine(Path.GetTempPath(), "benheim-runtime-failures-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testRoot);
try
{
    File.WriteAllText(
        Path.Combine(testRoot, "LogOutput.log"),
        "[Info   :   BepInEx] startup\n" +
        "[Warning:   BepInEx] ordinary compatibility notice\n" +
        "[Error  :   BepInEx] Could not load [Broken 1.0], token=super-secret authorization: Bearer bearer-secret from /Users/alice/Valheim/Broken.dll at 192.168.1.5 peer 76561198000000000\n" +
        "System.TypeLoadException: missing type\n" +
        "  at /Users/alice/source/Broken.cs:42\n" +
        "[Error  : Unity Log] DllNotFoundException: BrokenNative\n" +
        "Stack trace:\n" +
        "Broken.Native.Call ()\n" +
        "[Info   : Unity Log] startup continues\n");

    RuntimeFailureCapture.Begin(testRoot);
    ExpectEqual(1, Logger.Listeners.Count, "one BepInEx listener is registered");
    RuntimeFailureCapture.Update();
    ExpectEqual(2, Diagnostics.Emitted.Count, "startup BepInEx and Unity errors become typed records");

    JsonElement pluginFailure = Parse(Diagnostics.Emitted[0]);
    ExpectEqual("Health", pluginFailure.GetProperty("domain").GetString(), "failure domain");
    ExpectEqual("runtime_failure", pluginFailure.GetProperty("event").GetString(), "failure event");
    ExpectEqual("bepinex", pluginFailure.GetProperty("failure_source").GetString(), "BepInEx source");
    ExpectEqual("startup_log", pluginFailure.GetProperty("capture_origin").GetString(), "startup origin");
    string pluginMessage = pluginFailure.GetProperty("message").GetString() ?? string.Empty;
    string pluginStack = pluginFailure.GetProperty("stack_trace").GetString() ?? string.Empty;
    Expect(pluginMessage.Contains("token=[redacted]"), "credential value is redacted");
    Expect(pluginMessage.Contains("authorization=[redacted]"), "Bearer credential is redacted");
    Expect(pluginMessage.Contains("<local_path>"), "local path is redacted");
    Expect(pluginMessage.Contains("<ip>"), "IP address is redacted");
    Expect(pluginMessage.Contains("<identifier>"), "long account identifier is redacted");
    Expect(!pluginMessage.Contains("super-secret"), "credential does not survive");
    Expect(!pluginMessage.Contains("bearer-secret"), "Bearer value does not survive");
    Expect(!pluginStack.Contains("/Users/alice"), "stack source path is redacted");

    JsonElement unityFailure = Parse(Diagnostics.Emitted[1]);
    ExpectEqual("unity", unityFailure.GetProperty("failure_source").GetString(), "Unity source");
    ExpectEqual("error", unityFailure.GetProperty("severity").GetString(), "severity is queryable");
    ExpectEqual("DllNotFoundException: BrokenNative", unityFailure.GetProperty("message").GetString(), "Unity message");

    ILogListener listener = Logger.Listeners[0];
    TestLogSource pluginSource = new TestLogSource("ExamplePlugin");
    listener.LogEvent(
        pluginSource,
        new LogEventArgs("Cache warmed normally", LogLevel.Warning, pluginSource));
    listener.LogEvent(
        pluginSource,
        new LogEventArgs("Runtime hook failed: https://private.example/path?token=secret", LogLevel.Warning, pluginSource));
    listener.LogEvent(
        pluginSource,
        new LogEventArgs("Runtime hook failed: https://private.example/path?token=secret", LogLevel.Warning, pluginSource));
    RuntimeFailureCapture.Update();
    ExpectEqual(3, Diagnostics.Emitted.Count, "actionable warning is captured once and routine warning is ignored");
    JsonElement liveFailure = Parse(Diagnostics.Emitted[2]);
    ExpectEqual("live", liveFailure.GetProperty("capture_origin").GetString(), "live origin");
    ExpectEqual("warning", liveFailure.GetProperty("severity").GetString(), "warning severity");
    ExpectEqual(
        "Runtime hook failed: <url>",
        liveFailure.GetProperty("message").GetString(),
        "URLs and query credentials are not forwarded");

    string longMessage = "Fatal runtime " + new string('x', RuntimeFailureCapture.MaximumMessageCharacters * 2);
    listener.LogEvent(pluginSource, new LogEventArgs(longMessage, LogLevel.Fatal, pluginSource));
    RuntimeFailureCapture.Update();
    JsonElement bounded = Parse(Diagnostics.Emitted[3]);
    ExpectEqual(
        RuntimeFailureCapture.MaximumMessageCharacters,
        (bounded.GetProperty("message").GetString() ?? string.Empty).Length,
        "message length is bounded");

    int existingFailures = CountEvents("runtime_failure");
    for (int index = 0; index < RuntimeFailureCapture.MaximumDistinctFailures + 20; index++)
    {
        listener.LogEvent(
            pluginSource,
            new LogEventArgs("Unique failure " + index.ToString(), LogLevel.Error, pluginSource));
    }
    for (int index = 0; index < 20; index++)
    {
        RuntimeFailureCapture.Update();
    }

    ExpectEqual(
        RuntimeFailureCapture.MaximumDistinctFailures,
        CountEvents("runtime_failure"),
        "session-wide distinct failure cardinality is bounded");
    ExpectEqual(1, CountEvents("runtime_failure_capture_limited"), "one limit record explains omitted failures");
    Expect(existingFailures < RuntimeFailureCapture.MaximumDistinctFailures, "test exercised remaining capacity");

    RuntimeFailureCapture.End();
    ExpectEqual(0, Logger.Listeners.Count, "listener is detached during teardown");
}
finally
{
    RuntimeFailureCapture.End();
    Directory.Delete(testRoot, recursive: true);
}

Console.WriteLine("runtime failure capture checks passed");

static JsonElement Parse(string json)
{
    using JsonDocument document = JsonDocument.Parse(json);
    return document.RootElement.Clone();
}

static int CountEvents(string name)
{
    int count = 0;
    foreach (string json in Diagnostics.Emitted)
    {
        if (Parse(json).GetProperty("event").GetString() == name)
        {
            count++;
        }
    }
    return count;
}

static void Expect(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void ExpectEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
