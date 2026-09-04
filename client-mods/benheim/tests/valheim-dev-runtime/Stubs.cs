using System;
using System.Collections.Generic;
using System.Text;

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

    internal sealed class DiagnosticEvent
    {
        private readonly Dictionary<string, string> fields = new Dictionary<string, string>();
        private DiagnosticEvent(string domain, string name)
        {
            Domain = domain;
            Name = name;
        }

        internal string Domain { get; }
        internal string Name { get; }
        internal static DiagnosticEvent Create(string domain, string name) => new DiagnosticEvent(domain, name);
        internal DiagnosticEvent String(string name, string? value)
        {
            fields[name] = value ?? string.Empty;
            return this;
        }
        internal string ToJsonLine()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("{\"domain\":\"").Append(Domain).Append("\",\"event\":\"").Append(Name).Append("\"");
            foreach (KeyValuePair<string, string> field in fields)
            {
                builder.Append(",\"").Append(field.Key).Append("\":\"").Append(field.Value).Append("\"");
            }
            return builder.Append('}').ToString();
        }
    }

    internal static class Diagnostics
    {
        private static Action<DiagnosticEvent>? observer;
        internal static string Flatten(string value) => value.Replace('\r', ' ').Replace('\n', ' ');
        internal static void SetValheimDevObserver(Action<DiagnosticEvent>? value) => observer = value;
        internal static void Emit(DiagnosticEvent value) => observer?.Invoke(value);
    }
}
