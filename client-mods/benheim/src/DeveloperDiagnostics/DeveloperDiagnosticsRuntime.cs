using System;
using System.Collections.Generic;
using BenheimQoL.EnemyTiers;
using BenheimQoL.Farming;
using BenheimQoL.Infrastructure;
using BenheimQoL.Interaction;
using BenheimQoL.Spawning;
using BenheimQoL.WispEcho;

namespace BenheimQoL.DeveloperDiagnostics;

/// <summary>
/// Owns discovery, command execution, session state, cleanup, and failure
/// containment for Benheim's shipped developer diagnostics.
/// </summary>
internal static partial class DeveloperDiagnosticsRuntime
{
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
            ["wispecho"] = WispEchoDiscovery.Run,
        };

    private static readonly Dictionary<string, RegisteredProbe> Probes =
        new(StringComparer.OrdinalIgnoreCase);

    private static bool builtInsRegistered;
    private static bool commandRegistered;
    private static bool worldReady;

    internal static void RegisterEventProbe(
        string name,
        bool shippedDefault,
        DiagnosticProbeActivation setActive,
        Action update,
        Action<DiagnosticProbeCleanupReason> cleanup)
    {
        RegisterProbe(
            name,
            DiagnosticProbeKind.Event,
            shippedDefault,
            setActive,
            update,
            cleanup);
    }

    internal static void DisableEventProbe(string name)
    {
        // Bounded captures finish through the owner of effective state, so a
        // timed-out probe cannot keep reporting "on" or restart on world entry.
        if (Probes.TryGetValue(name, out RegisteredProbe? probe) &&
            probe.Kind == DiagnosticProbeKind.Event)
        {
            probe.Override = ProbeSessionOverride.Off;
            // Cleanup callbacks may also end a one-shot session after world
            // exit or failure. Active is already false then; do not reenter
            // cleanup merely to update the remaining session override.
            if (probe.Active)
            {
                Deactivate(probe, "capture_cleanup", DiagnosticProbeCleanupReason.Disabled);
            }
        }
    }

    internal static void InitializeConsole()
    {
        SpawnPopulationProbe.Register();
        EnsureBuiltInsRegistered();
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
                $"bhrun <{string.Join("|", SnapshotNames())}>",
                Snapshots,
                args),
            isCheat: false,
            isNetwork: false,
            optionsFetcher: SnapshotNames);
        _ = new Terminal.ConsoleCommand(
            "bhwatch",
            "inspect or change a session-only diagnostic probe; run 'bhwatch' for status",
            ExecuteWatcher,
            isCheat: false,
            isNetwork: false,
            optionsFetcher: ProbeNames);
        commandRegistered = true;
    }

    internal static void Update()
    {
        EnsureBuiltInsRegistered();
        SynchronizeWorld(IsWorldReady());
        foreach (RegisteredProbe probe in Probes.Values)
        {
            if (!probe.Active)
            {
                continue;
            }

            try
            {
                probe.Update();
            }
            catch (Exception exception)
            {
                Deactivate(
                    probe,
                    "update_cleanup",
                    DiagnosticProbeCleanupReason.Failure);
                ReportFailure("update", probe.Name, exception.Message);
            }
        }
    }

    internal static void Reset()
    {
        EnsureBuiltInsRegistered();
        foreach (RegisteredProbe probe in Probes.Values)
        {
            Deactivate(
                probe,
                "session_cleanup",
                DiagnosticProbeCleanupReason.SessionReset);
            probe.Override = ProbeSessionOverride.Default;
        }
        worldReady = false;
    }

    internal static void ReportFailure(
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

    private static void EnsureBuiltInsRegistered()
    {
        if (builtInsRegistered)
        {
            return;
        }

        builtInsRegistered = true;
        RegisterEventProbe(
            FarmingInputProbe.Name,
            shippedDefault: false,
            FarmingInputProbe.TrySetActive,
            FarmingInputProbe.Update,
            FarmingInputProbe.Cleanup);
        RegisterProbe(
            "colliders",
            DiagnosticProbeKind.Visual,
            shippedDefault: false,
            CharacterColliderOverlay.TrySetActive,
            CharacterColliderOverlay.Update,
            _ => CharacterColliderOverlay.Reset());
    }

    private static void RegisterProbe(
        string name,
        DiagnosticProbeKind kind,
        bool shippedDefault,
        DiagnosticProbeActivation setActive,
        Action update,
        Action<DiagnosticProbeCleanupReason> cleanup)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A diagnostic probe must have a name.", nameof(name));
        }
        if (Probes.ContainsKey(name))
        {
            throw new InvalidOperationException($"Diagnostic probe '{name}' is already registered.");
        }

        RegisteredProbe probe = new(
            name,
            kind,
            shippedDefault,
            setActive,
            update,
            cleanup);
        Probes.Add(name, probe);
        if (worldReady)
        {
            Reconcile(probe, context: null);
        }
    }

    private static object ExecuteSnapshot(
        string command,
        string usage,
        Dictionary<string, Action<string[], Action<string>>> snapshots,
        Terminal.ConsoleEventArgs args)
    {
        if (args.Args.Length < 2 ||
            !snapshots.TryGetValue(args.Args[1], out Action<string[], Action<string>>? run))
        {
            args.Context.AddString($"Usage: {usage}");
            args.Context.AddString($"Available probes: {string.Join(", ", snapshots.Keys)}");
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
        EnsureBuiltInsRegistered();
        SynchronizeWorld(IsWorldReady());
        if (args.Args.Length == 1)
        {
            foreach (string name in ProbeNames())
            {
                PrintProbeStatus(args.Context, Probes[name]);
            }
            return true;
        }

        if (args.Args.Length < 2 || args.Args.Length > 3 ||
            !Probes.TryGetValue(args.Args[1], out RegisteredProbe? probe))
        {
            PrintWatcherUsage(args.Context);
            return true;
        }

        if (args.Args.Length == 3)
        {
            if (!TryParseOverride(args.Args[2], out ProbeSessionOverride requestedOverride))
            {
                PrintWatcherUsage(args.Context);
                return true;
            }
            probe.Override = requestedOverride;
            Reconcile(probe, args.Context);
        }

        PrintProbeStatus(args.Context, probe);
        return true;
    }

    private static void SynchronizeWorld(bool isWorldReady)
    {
        if (worldReady == isWorldReady)
        {
            return;
        }

        worldReady = isWorldReady;
        foreach (RegisteredProbe probe in Probes.Values)
        {
            if (worldReady)
            {
                Reconcile(probe, context: null);
            }
            else
            {
                Deactivate(
                    probe,
                    "world_cleanup",
                    DiagnosticProbeCleanupReason.WorldExit);
            }
        }
    }

    private static bool IsWorldReady()
    {
        // Valheim destroys and recreates the local Player during ordinary
        // death and respawn while the loaded world and SpawnSystem remain.
        // ZNetScene owns the actual world lifetime, so only its teardown may
        // trigger WorldExit cleanup and discard registered world probes.
        return ZNetScene.instance != null;
    }

    private static void Reconcile(RegisteredProbe probe, Terminal? context)
    {
        bool configured = probe.Override switch
        {
            ProbeSessionOverride.On => true,
            ProbeSessionOverride.Off => false,
            _ => probe.ShippedDefault,
        };
        bool desired = worldReady && configured;
        if (desired == probe.Active)
        {
            return;
        }

        try
        {
            if (probe.SetActive(desired, out string failure))
            {
                probe.Active = desired;
                if (!desired)
                {
                    Cleanup(
                        probe,
                        "state_cleanup",
                        DiagnosticProbeCleanupReason.Disabled);
                }
                return;
            }

            probe.Active = false;
            string reason = Flatten(failure);
            context?.AddString(
                $"Benheim probe {probe.Name} could not turn {(desired ? "on" : "off")}: {reason}");
            ReportFailure("probe_state", probe.Name, reason);
            Cleanup(
                probe,
                "state_cleanup",
                DiagnosticProbeCleanupReason.Failure);
        }
        catch (Exception exception)
        {
            probe.Active = false;
            string reason = Flatten(exception.Message);
            context?.AddString($"Benheim probe {probe.Name} failed: {reason}");
            ReportFailure("probe_state", probe.Name, reason);
            Cleanup(
                probe,
                "failure_cleanup",
                DiagnosticProbeCleanupReason.Failure);
        }
    }

    private static void Deactivate(
        RegisteredProbe probe,
        string lifecycle,
        DiagnosticProbeCleanupReason reason)
    {
        if (probe.Active)
        {
            try
            {
                if (!probe.SetActive(false, out string failure))
                {
                    ReportFailure(lifecycle, probe.Name, failure);
                }
            }
            catch (Exception exception)
            {
                ReportFailure(lifecycle, probe.Name, exception.Message);
            }
        }
        probe.Active = false;
        Cleanup(probe, lifecycle, reason);
    }

    private static void Cleanup(
        RegisteredProbe probe,
        string lifecycle,
        DiagnosticProbeCleanupReason reason)
    {
        try
        {
            probe.Cleanup(reason);
        }
        catch (Exception exception)
        {
            ReportFailure(lifecycle, probe.Name, exception.Message);
        }
    }

    private static void PrintWatcherUsage(Terminal context)
    {
        context.AddString("Usage: bhwatch [<probe> [on|off|default]]");
        context.AddString($"Available probes: {string.Join(", ", ProbeNames())}");
    }

    private static void PrintProbeStatus(Terminal context, RegisteredProbe probe)
    {
        context.AddString(
            $"Benheim probe {probe.Name}: " +
            $"kind={probe.Kind.ToString().ToLowerInvariant()} " +
            $"default={StateName(probe.ShippedDefault)} " +
            $"override={probe.Override.ToString().ToLowerInvariant()} " +
            $"effective={StateName(probe.Active)}");
    }

}
