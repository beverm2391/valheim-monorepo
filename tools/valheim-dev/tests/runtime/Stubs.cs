using System;
using System.Collections.Generic;

public sealed class Terminal
{
    public sealed class ConsoleEventArgs
    {
        public ConsoleEventArgs(string line, Terminal context)
        {
            Args = line.Split(' ');
            Context = context;
        }

        public string[] Args { get; }
        public Terminal Context { get; }
    }

    public delegate object ConsoleEventFailable(ConsoleEventArgs args);

    public sealed class ConsoleCommand
    {
        private readonly ConsoleEventFailable action;

        public ConsoleCommand(
            string command,
            string description,
            ConsoleEventFailable action,
            bool isCheat = false,
            bool isNetwork = false)
        {
            this.action = action;
            commands[command.ToLowerInvariant()] = this;
        }

        public bool IsValid(Terminal context) => true;
        public void RunAction(ConsoleEventArgs args) => action(args);
    }

    private static readonly Dictionary<string, ConsoleCommand> commands =
        new Dictionary<string, ConsoleCommand>(StringComparer.OrdinalIgnoreCase);

    internal readonly List<string> Lines = new List<string>();
    public void AddString(string value) => Lines.Add(value);
    internal static ConsoleCommand Command(string name) => commands[name];
    internal static void ClearCommands() => commands.Clear();
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
        ValheimDev.ValheimDevDiagnostics.PublishEvidenceForTests(
            "Test",
            "synchronous_run",
            "{\"domain\":\"Test\",\"event\":\"synchronous_run\"}");
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

    public static class Paths
    {
        public static string BepInExRootPath => System.IO.Path.GetTempPath();
    }
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

namespace ValheimDev
{
    internal static class Plugin
    {
        internal static TestLog Log { get; } = new TestLog();
    }

    internal sealed class TestLog
    {
        internal readonly List<string> Errors = new List<string>();
        internal readonly List<string> Warnings = new List<string>();
        internal readonly List<string> Infos = new List<string>();
        internal void LogError(string value) => Errors.Add(value);
        internal void LogWarning(string value) => Warnings.Add(value);
        internal void LogInfo(string value) => Infos.Add(value);
    }
}
