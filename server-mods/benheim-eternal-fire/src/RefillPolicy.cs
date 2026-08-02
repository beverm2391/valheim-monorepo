using System;

namespace BenheimEternalFire;

internal static class RefillPolicy
{
    // Fireplace.IsBurning requires fuel > 0. One native fuel unit therefore
    // leaves a full m_secPerFuel interval for the server correction to reach
    // the simulating client without rewriting every two-second update.
    internal const float LowFuelThreshold = 1f;

    internal static bool ShouldRefill(float currentFuel, float maxFuel)
    {
        if (float.IsNaN(currentFuel) || float.IsInfinity(currentFuel) ||
            float.IsNaN(maxFuel) || float.IsInfinity(maxFuel) || maxFuel <= 0f)
        {
            return false;
        }

        float effectiveThreshold = Math.Min(LowFuelThreshold, maxFuel);
        return currentFuel < maxFuel && currentFuel <= effectiveThreshold;
    }
}
