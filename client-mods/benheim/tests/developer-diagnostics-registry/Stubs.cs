using System;
using System.Collections.Generic;

namespace BenheimQoL
{
    internal static class Plugin
    {
        internal static TestLog Log { get; } = new();
    }

    internal sealed class TestLog
    {
        internal List<string> Errors { get; } = new();

        internal void LogError(object value)
        {
            Errors.Add(value.ToString() ?? string.Empty);
        }
    }
}

public sealed class Player
{
    public static Player? m_localPlayer;
}

public sealed class ZNetScene
{
    public static ZNetScene? instance;
}

public sealed class Terminal
{
    public delegate object ConsoleEventFailable(ConsoleEventArgs args);
    public delegate List<string> ConsoleOptionsFetcher();

    public static Dictionary<string, ConsoleCommand> Commands { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<string> Lines { get; } = new();

    public void AddString(string value)
    {
        Lines.Add(value);
    }

    public sealed class ConsoleEventArgs
    {
        public ConsoleEventArgs(string[] args, Terminal context)
        {
            Args = args;
            Context = context;
        }

        public string[] Args { get; }
        public Terminal Context { get; }
    }

    public sealed class ConsoleCommand
    {
        private readonly ConsoleEventFailable action;
        private readonly ConsoleOptionsFetcher? optionsFetcher;

        public ConsoleCommand(
            string command,
            string description,
            ConsoleEventFailable action,
            bool isCheat = false,
            bool isNetwork = false,
            bool onlyServer = false,
            bool isSecret = false,
            bool allowInDevBuild = false,
            ConsoleOptionsFetcher? optionsFetcher = null,
            bool alwaysRefreshTabOptions = false,
            bool remoteCommand = false,
            bool onlyAdmin = false)
        {
            Command = command;
            Description = description;
            this.action = action;
            this.optionsFetcher = optionsFetcher;
            Commands[command] = this;
        }

        public string Command { get; }
        public string Description { get; }

        public List<string>? GetTabOptions() => optionsFetcher?.Invoke();

        public object Run(string[] arguments, Terminal context) =>
            action(new ConsoleEventArgs(arguments, context));
    }
}

namespace BenheimQoL.Infrastructure
{
    internal enum RuntimePrimitiveCatalogCategory
    {
        Effects,
        Text,
        Ui,
    }

    internal static class RuntimePrimitiveCatalogCommand
    {
        internal static RuntimePrimitiveCatalogCategory LastCategory { get; private set; }
        internal static string[] LastArguments { get; private set; } = Array.Empty<string>();
        internal static int RunCount { get; private set; }

        internal static void Run(
            RuntimePrimitiveCatalogCategory category,
            string[] arguments,
            Action<string> output)
        {
            LastCategory = category;
            LastArguments = arguments;
            RunCount++;
            output($"catalog:{category}");
        }
    }

    internal sealed class DiagnosticEvent
    {
        internal static DiagnosticEvent Create(string domain, string name) => new();

        internal DiagnosticEvent String(string key, string value) => this;
    }

    internal static class Diagnostics
    {
        internal static List<DiagnosticEvent> Events { get; } = new();

        internal static string Flatten(string value) => value;

        internal static void Emit(DiagnosticEvent diagnosticEvent)
        {
            Events.Add(diagnosticEvent);
        }
    }
}

namespace BenheimQoL.Interaction
{
    internal static class ComfortDiagnosticCommand
    {
        internal static int RunCount { get; private set; }
        internal static bool ThrowOnRun { get; set; }

        internal static void Run(string[] arguments, Action<string> output)
        {
            RunCount++;
            if (ThrowOnRun)
            {
                throw new InvalidOperationException("comfort exploded\nwithout escaping");
            }
            output("comfort snapshot ran");
        }
    }
}

namespace BenheimQoL.EnemyTiers
{
    internal static class CharacterColliderOverlay
    {
        internal static bool Active { get; private set; }
        internal static int EnableCount { get; private set; }
        internal static int DisableCount { get; private set; }
        internal static int ResetCount { get; private set; }
        internal static int UpdateCount { get; private set; }
        internal static int OwnedResourceCount { get; private set; }
        internal static bool ThrowOnUpdate { get; set; }

        internal static bool TrySetActive(bool active, out string failure)
        {
            failure = string.Empty;
            Active = active;
            if (active)
            {
                EnableCount++;
            }
            else
            {
                DisableCount++;
            }
            return true;
        }

        internal static void Update()
        {
            UpdateCount++;
            if (ThrowOnUpdate)
            {
                ThrowOnUpdate = false;
                OwnedResourceCount++;
                throw new InvalidOperationException("collider update exploded");
            }
        }

        internal static void Reset()
        {
            Active = false;
            OwnedResourceCount = 0;
            ResetCount++;
        }
    }
}

namespace BenheimQoL.Spawning
{
    using BenheimQoL.DeveloperDiagnostics;

    internal static class SpawnPopulationProbe
    {
        private static bool registered;

        internal static void Register()
        {
            if (registered)
            {
                return;
            }

            DeveloperDiagnosticsRuntime.RegisterEventProbe(
                "spawns",
                shippedDefault: true,
                TestSpawnProbe.TrySetActive,
                TestSpawnProbe.Update,
                TestSpawnProbe.Cleanup);
            registered = true;
        }
    }

    internal static class TestSpawnProbe
    {
        internal static bool Active { get; private set; }
        internal static bool RejectActivation { get; set; }
        internal static bool ThrowOnUpdate { get; set; }
        internal static int EnableCount { get; private set; }
        internal static int DisableCount { get; private set; }
        internal static int CleanupCount { get; private set; }

        internal static bool TrySetActive(bool requested, out string failure)
        {
            failure = string.Empty;
            Active = requested;
            if (requested)
            {
                EnableCount++;
            }
            else
            {
                DisableCount++;
            }
            if (requested && RejectActivation)
            {
                failure = "activation rejected after allocation";
                return false;
            }
            return true;
        }

        internal static void Update()
        {
            if (!ThrowOnUpdate)
            {
                return;
            }

            ThrowOnUpdate = false;
            throw new InvalidOperationException("spawn probe exploded");
        }

        internal static void Cleanup(DiagnosticProbeCleanupReason _)
        {
            Active = false;
            CleanupCount++;
        }
    }
}
