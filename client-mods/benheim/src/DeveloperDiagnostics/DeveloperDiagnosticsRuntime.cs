using System;
using System.Collections.Generic;
using BenheimQoL.EnemyTiers;
using BenheimQoL.Infrastructure;
using BenheimQoL.Interaction;

namespace BenheimQoL.DeveloperDiagnostics;

internal enum WatcherSessionSetting
{
    Default,
    On,
    Off,
}

/// <summary>
/// Owns discovery, command execution, session state, and cleanup for Benheim's
/// shipped developer-only snapshots and collider watcher.
/// </summary>
internal static class DeveloperDiagnosticsRuntime
{
    private const bool ColliderShippedDefault = false;

    private static readonly Dictionary<string, Action<string[], Action<string>>> Catalogs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["effects"] = (arguments, output) => RuntimePrimitiveCatalogCommand.Run(
                RuntimePrimitiveCatalogCategory.Effects,
                arguments,
                output),
            ["text"] = (arguments, output) => RuntimePrimitiveCatalogCommand.Run(
                RuntimePrimitiveCatalogCategory.Text,
                arguments,
                output),
            ["ui"] = (arguments, output) => RuntimePrimitiveCatalogCommand.Run(
                RuntimePrimitiveCatalogCategory.Ui,
                arguments,
                output),
        };

    private static readonly Dictionary<string, Action<string[], Action<string>>> Snapshots =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["comfort"] = ComfortDiagnosticCommand.Run,
        };

    private static bool commandRegistered;
    private static bool worldReady;
    private static bool colliderActive;
    private static WatcherSessionSetting colliderSetting;

    internal static void InitializeConsole()
    {
        if (commandRegistered)
        {
            return;
        }

        _ = new Terminal.ConsoleCommand(
            "bhcatalog",
            "list a bounded runtime catalog; run 'bhcatalog' for choices",
            args => ExecuteSnapshot(
                "bhcatalog",
                "bhcatalog <effects|text|ui> [filter]",
                Catalogs,
                args),
            isCheat: false,
            isNetwork: false,
            optionsFetcher: CatalogNames);
        _ = new Terminal.ConsoleCommand(
            "bhrun",
            "run one developer diagnostic snapshot; run 'bhrun' for choices",
            args => ExecuteSnapshot(
                "bhrun",
                "bhrun <comfort>",
                Snapshots,
                args),
            isCheat: false,
            isNetwork: false,
            optionsFetcher: SnapshotNames);
        _ = new Terminal.ConsoleCommand(
            "bhwatch",
            "inspect or change a session-only diagnostic watcher; run 'bhwatch' for status",
            ExecuteWatcher,
            isCheat: false,
            isNetwork: false,
            optionsFetcher: WatcherNames);
        commandRegistered = true;
    }

    internal static void Update()
    {
        SynchronizeWorld(Player.m_localPlayer != null);
        if (!colliderActive)
        {
            return;
        }

        try
        {
            CharacterColliderOverlay.Update();
        }
        catch (Exception exception)
        {
            ResetCollider("update_cleanup");
            ReportFailure("update", "colliders", exception.Message);
        }
    }

    internal static void Reset()
    {
        ResetCollider("session_cleanup");
        colliderSetting = WatcherSessionSetting.Default;
        worldReady = false;
    }

    private static object ExecuteSnapshot(
        string command,
        string usage,
        Dictionary<string, Action<string[], Action<string>>> probes,
        Terminal.ConsoleEventArgs args)
    {
        if (args.Args.Length < 2 ||
            !probes.TryGetValue(args.Args[1], out Action<string[], Action<string>>? run))
        {
            args.Context.AddString($"Usage: {usage}");
            args.Context.AddString($"Available probes: {string.Join(", ", probes.Keys)}");
            return true;
        }

        try
        {
            run(Tail(args.Args, 2), args.Context.AddString);
        }
        catch (Exception exception)
        {
            string reason = Flatten(exception.Message);
            args.Context.AddString(
                $"Benheim {command} {args.Args[1]} failed: {reason}");
            ReportFailure(command, args.Args[1], reason);
        }
        return true;
    }

    private static object ExecuteWatcher(Terminal.ConsoleEventArgs args)
    {
        SynchronizeWorld(Player.m_localPlayer != null);
        if (args.Args.Length == 1)
        {
            PrintColliderStatus(args.Context);
            return true;
        }

        if (args.Args.Length < 2 || args.Args.Length > 3 ||
            !string.Equals(args.Args[1], "colliders", StringComparison.OrdinalIgnoreCase))
        {
            PrintWatcherUsage(args.Context);
            return true;
        }

        if (args.Args.Length == 3)
        {
            if (!TryParseSetting(args.Args[2], out colliderSetting))
            {
                PrintWatcherUsage(args.Context);
                return true;
            }
            ReconcileCollider(args.Context);
        }

        PrintColliderStatus(args.Context);
        return true;
    }

    private static void SynchronizeWorld(bool isWorldReady)
    {
        if (worldReady == isWorldReady)
        {
            return;
        }

        worldReady = isWorldReady;
        if (worldReady)
        {
            ReconcileCollider(context: null);
        }
        else
        {
            ResetCollider("world_cleanup");
        }
    }

    private static void ReconcileCollider(Terminal? context)
    {
        bool desired = colliderSetting switch
        {
            WatcherSessionSetting.On => true,
            WatcherSessionSetting.Off => false,
            _ => ColliderShippedDefault,
        };
        bool shouldBeActive = worldReady && desired;
        if (shouldBeActive == colliderActive)
        {
            return;
        }

        try
        {
            if (CharacterColliderOverlay.TrySetActive(
                    shouldBeActive,
                    out string failure))
            {
                colliderActive = shouldBeActive;
                return;
            }

            colliderActive = false;
            string reason = Flatten(failure);
            context?.AddString(
                $"Benheim watcher colliders could not turn {(shouldBeActive ? "on" : "off")}: {reason}");
            ReportFailure("watcher_state", "colliders", reason);
            if (!shouldBeActive)
            {
                ResetCollider("state_cleanup");
            }
        }
        catch (Exception exception)
        {
            colliderActive = false;
            string reason = Flatten(exception.Message);
            context?.AddString($"Benheim watcher colliders failed: {reason}");
            ReportFailure("watcher_state", "colliders", reason);
            ResetCollider("failure_cleanup");
        }
    }

    private static void ResetCollider(string lifecycle)
    {
        try
        {
            CharacterColliderOverlay.Reset();
        }
        catch (Exception exception)
        {
            ReportFailure(lifecycle, "colliders", exception.Message);
        }
        colliderActive = false;
    }

    private static void PrintWatcherUsage(Terminal context)
    {
        context.AddString("Usage: bhwatch [<watcher> [on|off|default]]");
        context.AddString("Available watchers: colliders");
    }

    private static void PrintColliderStatus(Terminal context)
    {
        context.AddString(
            "Benheim watcher colliders: " +
            $"shipped={StateName(ColliderShippedDefault)} " +
            $"session={colliderSetting.ToString().ToLowerInvariant()} " +
            $"effective={StateName(colliderActive)}");
    }

    private static bool TryParseSetting(
        string value,
        out WatcherSessionSetting setting)
    {
        if (string.Equals(value, "default", StringComparison.OrdinalIgnoreCase))
        {
            setting = WatcherSessionSetting.Default;
            return true;
        }
        if (string.Equals(value, "on", StringComparison.OrdinalIgnoreCase))
        {
            setting = WatcherSessionSetting.On;
            return true;
        }
        if (string.Equals(value, "off", StringComparison.OrdinalIgnoreCase))
        {
            setting = WatcherSessionSetting.Off;
            return true;
        }

        setting = default;
        return false;
    }

    private static void ReportFailure(
        string lifecycle,
        string probe,
        string reason)
    {
        try
        {
            string flattenedReason = Diagnostics.Flatten(reason);
            Plugin.Log.LogError(
                $"Developer diagnostic {probe} failed during {lifecycle}: {flattenedReason}");
            Diagnostics.Emit(
                DiagnosticEvent.Create("DeveloperDiagnostics", "probe_failed")
                    .String("probe", probe)
                    .String("lifecycle", lifecycle)
                    .String("reason", flattenedReason));
        }
        catch
        {
            // Reporting a probe failure must not turn it into a gameplay failure.
        }
    }

    private static List<string> CatalogNames() => new(Catalogs.Keys);

    private static List<string> SnapshotNames() => new(Snapshots.Keys);

    private static List<string> WatcherNames() => new() { "colliders" };

    private static string[] Tail(string[] arguments, int start)
    {
        string[] tail = new string[Math.Max(0, arguments.Length - start)];
        Array.Copy(arguments, start, tail, 0, tail.Length);
        return tail;
    }

    private static string StateName(bool state) => state ? "on" : "off";

    private static string Flatten(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unknown failure"
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
