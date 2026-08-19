using BenheimQoL.Infrastructure;

namespace BenheimQoL.PlayerCombat;

internal static class PlayerCombatDiagnostics
{
    internal static void Project(PerfectDefenseConfirmed perfectDefense)
    {
        DiagnosticEvent diagnosticEvent =
            DiagnosticEvent.Create("PlayerCombat", "perfect_defense_confirmed")
                .String("defense", perfectDefense.Kind == PerfectDefenseKind.Parry ? "parry" : "dodge")
                .String("outcome_source", perfectDefense.OutcomeSource)
                .Integer("outcome_token", perfectDefense.OutcomeToken)
                .Number("health", perfectDefense.Context.Health)
                .Number("maximum_health", perfectDefense.Context.MaximumHealth)
                .Number("health_fraction", perfectDefense.Context.HealthFraction);

        if (perfectDefense.BlockTimer.HasValue)
        {
            diagnosticEvent.Number("block_timer", perfectDefense.BlockTimer.Value);
        }

        if (perfectDefense.TimedBlockBonus.HasValue)
        {
            diagnosticEvent.Number("timed_block_bonus", perfectDefense.TimedBlockBonus.Value);
        }

        Diagnostics.Emit(diagnosticEvent);
    }

    internal static void Project(AcceptedPlayerDamage damage)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "player_damage_accepted")
                .String(
                    "source",
                    damage.Source == AcceptedHealthLossSource.Damage
                        ? "damage"
                        : "health_cost")
                .Number("health_before", damage.Before.Health)
                .Number("health_after", damage.After.Health)
                .Number("health_lost", damage.HealthLost));
    }

    internal static void Project(ClutchDecision decision)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "clutch_decision")
                .String("defense", DefenseName(decision.Defense))
                .String("outcome", decision.Outcome.ToString())
                .String("reason", decision.Reason.ToString())
                .Number("health", decision.Context.Health)
                .Number("health_threshold", decision.HealthThreshold));
    }

    internal static void Project(UntouchableProgress progress)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "untouchable_streak_changed")
                .String("defense", DefenseName(progress.Defense))
                .String("outcome", progress.Outcome.ToString())
                .Integer("streak_before", progress.PreviousStreak)
                .Integer("streak_after", progress.CurrentStreak)
                .Integer("tier_before", progress.PreviousTier)
                .Integer("tier_after", progress.CurrentTier));
    }

    internal static void Project(UntouchableReset reset)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "untouchable_reset")
                .String("reason", reset.Reason.ToString())
                .Integer("streak_before", reset.PreviousStreak)
                .Integer("tier_before", reset.PreviousTier)
                .Number("health_before", reset.Damage.Before.Health)
                .Number("health_after", reset.Damage.After.Health)
                .Number("health_lost", reset.Damage.HealthLost));
    }

    internal static void Project(EarnedStateTransition transition)
    {
        string eventName = transition.Kind switch
        {
            EarnedStateTransitionKind.Activated => "earned_state_activated",
            EarnedStateTransitionKind.Refreshed => "earned_state_refreshed",
            EarnedStateTransitionKind.Expired => "earned_state_expired",
            EarnedStateTransitionKind.Removed => "earned_state_removed",
            _ => "earned_state_activation_rejected"
        };
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", eventName)
                .String("state", transition.State.ToString())
                .Integer("tier", transition.Tier)
                .String("reason", transition.Reason.ToString())
                .Number("health", transition.Context.Health)
                .Number("maximum_health", transition.Context.MaximumHealth));
    }

    internal static void Project(BerserkerChainTransition transition)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "berserker_chain_transition")
                .String("transition", transition.Kind.ToString())
                .String("tier", transition.Tier.ToString())
                .Integer("kill_count", transition.KillCount)
                .Integer("server_sequence", transition.ServerSequence)
                .Number("server_time_seconds", transition.ServerTimeSeconds)
                .Number(
                    "expires_at_server_time_seconds",
                    transition.ExpiresAtServerTimeSeconds));
    }

    internal static void Project(PlayerCombatEnded ended)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "player_combat_ended")
                .String("reason", ended.Reason.ToString()));
    }

    internal static void Project(PlayerCombatSessionEnded ended)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "combat_session_ended")
                .String("reason", ended.Reason.ToString()));
    }

    internal static void Project(ConfirmedKill confirmedKill)
    {
        Diagnostics.Emit(
            DiagnosticEvent.Create("PlayerCombat", "kill_confirmed")
                .String("killer_zdoid", confirmedKill.KillerId.ToString())
                .String("victim_zdoid", confirmedKill.VictimId.ToString())
                .String("victim_prefab", confirmedKill.VictimPrefabName)
                .Integer("victim_prefab_hash", confirmedKill.VictimPrefabHash)
                .Integer("victim_level", confirmedKill.VictimLevel)
                .Boolean("victim_boss", confirmedKill.VictimWasBoss)
                .Boolean("victim_tamed", confirmedKill.VictimWasTamed)
                .Number("kill_x", confirmedKill.KillPosition.x)
                .Number("kill_y", confirmedKill.KillPosition.y)
                .Number("kill_z", confirmedKill.KillPosition.z)
                .Integer("server_sequence", confirmedKill.ServerSequence)
                .Number("server_time_seconds", confirmedKill.ServerTimeSeconds));
    }

    private static string DefenseName(PerfectDefenseKind defense)
    {
        return defense == PerfectDefenseKind.Parry ? "parry" : "dodge";
    }
}
