namespace BenheimEternalFire;

/// <summary>
/// Owns capacities that the dedicated server cannot discover from its stripped
/// prefab registry. Keep this list narrower than the supported-piece list: a
/// normal prefab must continue to derive its capacity from the live component.
/// </summary>
internal static class MissingServerPrefabFuelCapacity
{
    internal static bool TryGet(string prefabName, out float maxFuel)
    {
        // Valheim 1.0 models the bathtub as a Smelter with max fuel 10 and no
        // ore queue. The Linux dedicated-server scene omits that prefab, while
        // the matching client retains it, so the server cannot read the native
        // serialized capacity. The ZDO fuel field remains the native s_fuel.
        if (prefabName == "piece_bathtub")
        {
            maxFuel = 10f;
            return true;
        }

        maxFuel = 0f;
        return false;
    }
}
