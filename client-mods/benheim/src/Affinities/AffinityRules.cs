using System;

namespace BenheimQoL.Affinities;

internal static class AffinityRules
{
    internal static AffinityLoadResult ReadStoredValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return AffinityLoadResult.None;
        return string.Equals(value, AffinityState.LungeValue, StringComparison.Ordinal)
            ? AffinityLoadResult.Lunge
            : AffinityLoadResult.Unsupported;
    }

    internal static bool IsEligibleClub(bool canonicalPrefab, int quality, int maximumQuality)
    {
        return canonicalPrefab && quality == maximumQuality;
    }

    internal static int CountConsumed(int before, int after)
    {
        return Math.Max(0, before - after);
    }

    internal static string ResolveLunge(
        bool owner,
        bool sameWeapon,
        bool hasLunge,
        bool grounded,
        bool swimming,
        bool flying,
        bool attached)
    {
        if (!owner) return "not_owner";
        if (!sameWeapon) return "weapon_changed";
        if (!hasLunge) return "affinity_missing";
        if (grounded) return "grounded";
        if (swimming) return "swimming";
        if (flying) return "flying";
        if (attached) return "attached";
        return "accepted";
    }

    internal static bool TryPlanarImpulse(
        float forwardX,
        float forwardZ,
        float force,
        out float impulseX,
        out float impulseZ)
    {
        float magnitude = (float)Math.Sqrt(forwardX * forwardX + forwardZ * forwardZ);
        if (magnitude <= 0.0001f)
        {
            impulseX = 0f;
            impulseZ = 0f;
            return false;
        }
        impulseX = forwardX / magnitude * force;
        impulseZ = forwardZ / magnitude * force;
        return true;
    }
}
