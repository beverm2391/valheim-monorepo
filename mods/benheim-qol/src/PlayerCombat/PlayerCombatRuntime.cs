using System;
using System.Collections.Generic;
using BenheimQoL.Adrenaline;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.PlayerCombat;

/// <summary>
/// Owns the local event bus, per-player controllers, and shared native output
/// adapters for the lifetime of one Benheim plugin session.
/// </summary>
internal static class PlayerCombatRuntime
{
    private static readonly Dictionary<Player, PlayerCombatController> Controllers =
        new Dictionary<Player, PlayerCombatController>();
    private static readonly EarnedStateEffectCatalog Effects =
        new EarnedStateEffectCatalog();
    private static readonly EarnedStatePresentation Presentation =
        new EarnedStatePresentation();
    private static readonly NativeEarnedStateOutput Output =
        new NativeEarnedStateOutput(Effects, Presentation);

    private static LocalGameEventBus? events;

    internal static void BeginSession()
    {
        if (events != null)
        {
            EndSession();
        }

        events = new LocalGameEventBus(LogSubscriberFailure);
        AdrenalineFeedback.Reset();

        // Controllers and native gameplay adapters subscribe before diagnostic
        // projections. Telemetry can fail, but it cannot cancel a decision.
        events.Subscribe<PerfectDefenseConfirmed>(ObservePerfectDefense);
        events.Subscribe<PerfectDefenseConfirmed>(AdrenalineFeedback.ObservePerfectDefense);
        events.Subscribe<PerfectDefenseConfirmed>(PlayerCombatDiagnostics.Project);

        events.Subscribe<AcceptedPlayerDamage>(ObserveAcceptedDamage);
        events.Subscribe<AcceptedPlayerDamage>(PlayerCombatDiagnostics.Project);

        events.Subscribe<PlayerCombatEnded>(ObservePlayerEnded);
        events.Subscribe<PlayerCombatEnded>(PlayerCombatDiagnostics.Project);

        events.Subscribe<PlayerCombatSessionEnded>(ObserveSessionEnded);
        events.Subscribe<PlayerCombatSessionEnded>(PlayerCombatDiagnostics.Project);

        events.Subscribe<ConfirmedKill>(PlayerCombatDiagnostics.Project);
    }

    internal static void ConfigureEffects(
        params EarnedStateEffectDefinition[] definitions)
    {
        ObjectDB? database = ObjectDB.instance;
        Effects.Unregister();
        Effects.Configure(definitions);
        if (database != null)
        {
            Effects.Register(database);
        }
    }

    internal static void RegisterNativeEffects(ObjectDB database)
    {
        Effects.Register(database);
    }

    internal static void Publish(PerfectDefenseConfirmed perfectDefense)
    {
        events?.Publish(perfectDefense);
    }

    internal static void Publish(AcceptedPlayerDamage damage)
    {
        events?.Publish(damage);
    }

    internal static void Publish(PlayerCombatEnded ended)
    {
        events?.Publish(ended);
    }

    /// <summary>
    /// The client kill-delivery adapter calls this after it validates that the
    /// server-confirmed killer is the current local player.
    /// </summary>
    internal static void Publish(ConfirmedKill confirmedKill)
    {
        events?.Publish(confirmedKill);
    }

    internal static void EndWorld()
    {
        events?.Publish(
            new PlayerCombatSessionEnded(PlayerCombatEndReason.WorldTeardown));
    }

    internal static void EndSession()
    {
        LocalGameEventBus? current = events;
        if (current == null)
        {
            ResetControllers();
            Effects.Unregister();
            return;
        }

        current.Publish(
            new PlayerCombatSessionEnded(PlayerCombatEndReason.PluginTeardown));
        current.Reset();
        events = null;
        Effects.Unregister();
        AdrenalineFeedback.Reset();
    }

    internal static void ObserveEffectStopped(
        Player player,
        EarnedCombatState state,
        int tier)
    {
        if (Controllers.TryGetValue(player, out PlayerCombatController? controller))
        {
            controller.ForgetStoppedOutput(state, tier);
        }
    }

    private static void ObservePerfectDefense(PerfectDefenseConfirmed perfectDefense)
    {
        GetOrCreate(perfectDefense.Context.Player).Observe(perfectDefense);
    }

    private static void ObserveAcceptedDamage(AcceptedPlayerDamage damage)
    {
        if (Controllers.TryGetValue(damage.After.Player, out PlayerCombatController? controller))
        {
            controller.Observe(damage);
        }
    }

    private static void ObservePlayerEnded(PlayerCombatEnded ended)
    {
        if (!Controllers.TryGetValue(ended.Player, out PlayerCombatController? controller))
        {
            return;
        }

        try
        {
            controller.Reset();
        }
        finally
        {
            Controllers.Remove(ended.Player);
        }
    }

    private static void ObserveSessionEnded(PlayerCombatSessionEnded _)
    {
        ResetControllers();
        Effects.Unregister();
    }

    private static PlayerCombatController GetOrCreate(Player player)
    {
        if (!Controllers.TryGetValue(player, out PlayerCombatController? controller))
        {
            controller = new PlayerCombatController(player, Output);
            Controllers.Add(player, controller);
        }

        return controller;
    }

    private static void ResetControllers()
    {
        PlayerCombatController[] current = new PlayerCombatController[Controllers.Count];
        Controllers.Values.CopyTo(current, 0);
        for (int index = 0; index < current.Length; index++)
        {
            try
            {
                current[index].Reset();
            }
            catch (Exception exception)
            {
                LogSubscriberFailure(typeof(PlayerCombatSessionEnded), exception);
            }
        }

        Controllers.Clear();
    }

    private static void LogSubscriberFailure(Type eventType, Exception exception)
    {
        Diagnostics.Event(
            "PlayerCombat",
            "game_event_subscriber_failed",
            $"event_type={eventType.Name} error={Diagnostics.Flatten(exception.Message)}");
    }
}
