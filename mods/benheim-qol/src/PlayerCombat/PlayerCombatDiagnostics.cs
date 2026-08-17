using BenheimQoL.Infrastructure;

namespace BenheimQoL.PlayerCombat;

internal static class PlayerCombatDiagnostics
{
    internal static void Project(PerfectDefenseConfirmed perfectDefense)
    {
        DiagnosticEvent diagnosticEvent =
            DiagnosticEvent.Create("PlayerCombat", "perfect_defense_confirmed")
                .String("defense", perfectDefense.Kind == PerfectDefenseKind.Parry ? "parry" : "dodge")
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
                .Number("health_before", damage.Before.Health)
                .Number("health_after", damage.After.Health)
                .Number("health_lost", damage.HealthLost));
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
}
