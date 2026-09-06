using System;
using System.Collections.Generic;

public sealed class Terminal
{
    internal readonly List<string> Lines = new List<string>();
    public void AddString(string value) => Lines.Add(value);
}

public sealed class ZRpc { }
public sealed class ZNetPeer { }

public sealed class ZNet
{
    public static ZNet? instance;
    public long GetWorldUID() => 1;
    public bool IsServer() => true;
    public static bool IsOpenServer() => false;
    public bool IsDedicated() => false;
    public List<ZNetPeer> GetPeers() => new List<ZNetPeer>();
    public ZRpc? GetServerRPC() => null;
}

public sealed class ZNetScene
{
    public static ZNetScene? instance;
}

public sealed class Player
{
    public static Player? m_localPlayer;
    public bool IsOwner() => true;
    public static implicit operator bool(Player? player) => player != null;
}

// The Good fixture calls this through its normal Run entrypoint so the runtime
// test can prove synchronously emitted evidence survives a zero-length window.
public static class ValheimDevTestEvidence
{
    public static void EmitSynchronousEvent()
    {
        BenheimQoL.Infrastructure.Diagnostics.Emit(
            BenheimQoL.Infrastructure.DiagnosticEvent.Create("Test", "synchronous_run"));
    }
}

// A tiny visible-state analogue for the first Affinity icon loop. Fixtures
// mutate this surface through their normal runtime entrypoints so lifecycle
// tests prove install, replace, restore, and removal rather than just JSON.
public static class ValheimDevTestSurface
{
    public static bool Visible { get; set; }
    public static string Variant { get; set; } = "baseline";
    public static int CleanupCount { get; set; }

    public static void Reset()
    {
        Visible = false;
        Variant = "baseline";
        CleanupCount = 0;
    }

    public static string Describe()
    {
        return "{\"target\":\"Affinity.weapon_icon\",\"component\":\"Image\",\"visible\":"
            + (Visible ? "true" : "false") + ",\"variant\":\"" + Variant + "\"}";
    }
}

namespace BepInEx
{
    public class BaseUnityPlugin { }
}

namespace HarmonyLib
{
    public sealed class Harmony { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string methodName) { }
    }
}

namespace BenheimQoL
{
    internal static class Plugin
    {
        internal static TestLog Log { get; } = new TestLog();
    }

    internal sealed class TestLog
    {
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal void LogError(string value) => Errors.Add(value);
        internal void LogWarning(string value) => Warnings.Add(value);
    }
}

namespace BenheimQoL.Infrastructure
{
    internal static class HealthReporting
    {
        internal static bool GameplayActionsEnabled { get; set; } = true;
    }

    internal static class Diagnostics
    {
        private static Action<DiagnosticEvent>? observer;
        private static readonly List<DiagnosticEvent> emitted = new List<DiagnosticEvent>();
        private static readonly DateTime TestTimestamp =
            new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc);
        internal static IReadOnlyList<DiagnosticEvent> EmittedForTests => emitted;
        internal static DiagnosticEvent? LastEmittedForTests { get; private set; }
        internal static string Flatten(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
        internal static void SetValheimDevObserver(Action<DiagnosticEvent>? value) => observer = value;
        internal static Action<DiagnosticEvent>? CaptureObserverForTests() => observer;
        internal static DiagnosticEvent PrepareForTests(DiagnosticEvent value)
        {
            value.Prepare(TestTimestamp, "test-diagnostic-session", "test-benheim");
            return value;
        }
        internal static void Emit(DiagnosticEvent value)
        {
            LastEmittedForTests = PrepareForTests(value);
            emitted.Add(value);
            observer?.Invoke(value);
        }
        internal static void ClearEmittedForTests()
        {
            emitted.Clear();
            LastEmittedForTests = null;
        }
    }
}
