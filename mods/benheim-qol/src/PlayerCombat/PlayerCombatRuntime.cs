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
        new NativeEarnedStateOutput(Effects);
    private static readonly RuntimeFactPublisher Facts =
        new RuntimeFactPublisher();

    private static LocalGameEventBus? events;

    internal static void BeginSession()
    {
        if (events != null)
        {
            EndSession();
        }

        events = new LocalGameEventBus(LogSubscriberFailure);
        PerfectDefenseObservation.Reset();
        AdrenalineFeedback.Reset();
        Presentation.Reset();
        Effects.Configure(
            ClutchMechanic.CreateEffectDefinition(),
            UntouchableMechanic.CreateEffectDefinition(1),
            UntouchableMechanic.CreateEffectDefinition(2),
            UntouchableMechanic.CreateEffectDefinition(3),
            BerserkerMechanic.CreateEffectDefinition(1),
            BerserkerMechanic.CreateEffectDefinition(2));

        // Controllers and native gameplay adapters subscribe before diagnostic
        // projections. Telemetry can fail, but it cannot cancel a decision.
        events.Subscribe<PerfectDefenseConfirmed>(ObservePerfectDefense);
        events.Subscribe<PerfectDefenseConfirmed>(AdrenalineFeedback.ObservePerfectDefense);
        events.Subscribe<PerfectDefenseConfirmed>(PlayerCombatDiagnostics.Project);

        events.Subscribe<ClutchDecision>(PlayerCombatDiagnostics.Project);
        events.Subscribe<UntouchableProgress>(PlayerCombatDiagnostics.Project);
        events.Subscribe<UntouchableReset>(PlayerCombatDiagnostics.Project);
        events.Subscribe<EarnedStateTransition>(Presentation.Observe);
        events.Subscribe<EarnedStateTransition>(PlayerCombatDiagnostics.Project);

        events.Subscribe<BerserkerChainTransition>(ObserveBerserkerTransition);
        events.Subscribe<BerserkerChainTransition>(PlayerCombatDiagnostics.Project);

        events.Subscribe<AcceptedPlayerDamage>(ObserveAcceptedDamage);
        events.Subscribe<AcceptedPlayerDamage>(PlayerCombatDiagnostics.Project);

        events.Subscribe<PlayerCombatEnded>(ObservePlayerEnded);
        events.Subscribe<PlayerCombatEnded>(PlayerCombatDiagnostics.Project);

        events.Subscribe<PlayerCombatSessionEnded>(ObserveSessionEnded);
        events.Subscribe<PlayerCombatSessionEnded>(PlayerCombatDiagnostics.Project);

        events.Subscribe<ConfirmedKill>(ObserveConfirmedKill);
        events.Subscribe<ConfirmedKill>(PlayerCombatDiagnostics.Project);
    }

    internal static void RegisterNativeEffects(ObjectDB database)
    {
        Effects.Register(database);
    }

    internal static void Publish(PerfectDefenseConfirmed perfectDefense)
    {
        events?.Publish(perfectDefense);
    }

    internal static void BeginPerfectDefensePresentation(PlayerCombatContext context)
    {
        Presentation.BeginPerfectDefense(context);
    }

    internal static void CompletePerfectDefensePresentation(
        Player player,
        string? adrenalineLine,
        bool nativeCharmActivated = false)
    {
        try
        {
            Presentation.CompletePerfectDefense(
                player,
                adrenalineLine,
                nativeCharmActivated);
        }
        catch (Exception exception)
        {
            Presentation.Reset();
            TryEmitDiagnostic(
                DiagnosticEvent.Create("PlayerCombat", "earned_state_presentation_failed")
                    .String("error", Diagnostics.Flatten(exception.Message)));
        }
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

    /// <summary>
    /// The server-chain adapter calls this after validating its
    /// authoritative transition for the current local killer.
    /// </summary>
    internal static void Publish(BerserkerChainTransition transition)
    {
        events?.Publish(transition);
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
            Effects.Reset();
            Presentation.Reset();
            PerfectDefenseObservation.Reset();
            return;
        }

        current.Publish(
            new PlayerCombatSessionEnded(PlayerCombatEndReason.PluginTeardown));
        current.Reset();
        events = null;
        Effects.Reset();
        Presentation.Reset();
        AdrenalineFeedback.Reset();
        PerfectDefenseObservation.Reset();
    }

    internal static void ObserveEffectStopped(
        Player player,
        EarnedCombatState state,
        int tier,
        bool expired)
    {
        if (Controllers.TryGetValue(player, out PlayerCombatController? controller)
            && controller.ForgetStoppedOutput(state, tier)
            && expired)
        {
            events?.Publish(
                new EarnedStateTransition(
                    PlayerCombatContext.Capture(player),
                    state,
                    tier,
                    EarnedStateTransitionKind.Expired,
                    EarnedStateTransitionReason.NativeDurationElapsed));
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

    private static void ObserveBerserkerTransition(BerserkerChainTransition transition)
    {
        GetOrCreate(transition.Context.Player).Observe(transition);
    }

    private static void ObserveConfirmedKill(ConfirmedKill confirmedKill)
    {
        GetOrCreate(confirmedKill.Killer.Player).Observe(confirmedKill);
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
            controller = new PlayerCombatController(player, Output, Facts);
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
        TryEmitDiagnostic(
            DiagnosticEvent.Create("PlayerCombat", "game_event_subscriber_failed")
                .String("event_type", eventType.Name)
                .String("error", Diagnostics.Flatten(exception.Message)));
    }

    private static void TryEmitDiagnostic(DiagnosticEvent diagnosticEvent)
    {
        try
        {
            Diagnostics.Emit(diagnosticEvent);
        }
        catch
        {
            // Diagnostic output cannot interrupt native callbacks, subscriber
            // isolation, or deterministic lifecycle cleanup.
        }
    }

    private sealed class RuntimeFactPublisher : IPlayerCombatFactPublisher
    {
        public void Publish(ClutchDecision decision)
        {
            events?.Publish(decision);
        }

        public void Publish(UntouchableProgress progress)
        {
            events?.Publish(progress);
        }

        public void Publish(UntouchableReset reset)
        {
            events?.Publish(reset);
        }

        public void Publish(EarnedStateTransition transition)
        {
            events?.Publish(transition);
        }
    }
}
