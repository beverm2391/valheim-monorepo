namespace BenheimQoL.KillAttribution;

/// <summary>
/// Captures direct-player provenance before Character.ApplyDamage can destroy
/// or disconnect the attacker. A candidate becomes a kill only when the same
/// authoritative call actually crosses the victim's health to zero.
/// </summary>
internal readonly struct LethalHitObservation
{
    private LethalHitObservation(
        bool eligible,
        float healthBefore,
        ZDOID victimId,
        ZDOID killerId)
    {
        Eligible = eligible;
        HealthBefore = healthBefore;
        VictimId = victimId;
        KillerId = killerId;
    }

    internal bool Eligible { get; }
    internal float HealthBefore { get; }
    internal ZDOID VictimId { get; }
    internal ZDOID KillerId { get; }

    internal static LethalHitObservation Capture(Character victim, HitData hit)
    {
        if (!victim.IsOwner()
            || victim.IsPlayer()
            || victim.GetHealth() <= 0f
            || !(hit.GetAttacker() is Player killer))
        {
            return default;
        }

        ZDOID victimId = victim.GetZDOID();
        ZDOID killerId = killer.GetZDOID();
        if (victimId.IsNone() || killerId.IsNone() || victimId == killerId)
        {
            return default;
        }

        return new LethalHitObservation(
            eligible: true,
            victim.GetHealth(),
            victimId,
            killerId);
    }

    internal bool BecameLethal(Character victim)
    {
        return Eligible
            && HealthBefore > 0f
            && victim.IsOwner()
            && victim.GetHealth() <= 0f;
    }
}
