using System;

namespace BenheimQoL.PlayerCombat;

internal enum AcceptedHealthLossSource
{
    Damage,
    HealthCost
}

internal sealed class AcceptedPlayerDamage
{
    internal AcceptedPlayerDamage(
        PlayerCombatContext before,
        PlayerCombatContext after,
        AcceptedHealthLossSource source = AcceptedHealthLossSource.Damage)
    {
        Before = before ?? throw new ArgumentNullException(nameof(before));
        After = after ?? throw new ArgumentNullException(nameof(after));
        Source = source;
        if (before.Player != after.Player)
        {
            throw new ArgumentException("Damage contexts must identify the same player.");
        }
    }

    internal PlayerCombatContext Before { get; }
    internal PlayerCombatContext After { get; }
    internal AcceptedHealthLossSource Source { get; }
    internal float HealthLost => Math.Max(0f, Before.Health - After.Health);
}
