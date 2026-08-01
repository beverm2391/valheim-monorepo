using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BenheimEternalFire;

internal static class ZdoFuelNormalizer
{
    private static readonly HashSet<int> LoggedMissingPrefabs = new HashSet<int>();

    internal static void Normalize(ZDO zdo, string source)
    {
        if (ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        int prefabHash = zdo.GetPrefab();
        if (!SupportedFireplaces.TryGetName(prefabHash, out string prefabName))
        {
            return;
        }

        GameObject? prefab = ZNetScene.instance?.GetPrefab(prefabHash);
        Fireplace? fireplace = prefab?.GetComponent<Fireplace>();
        if (fireplace == null || fireplace.m_maxFuel <= 0f)
        {
            if (LoggedMissingPrefabs.Add(prefabHash))
            {
                Plugin.Log.LogWarning(
                    $"[diag][EternalFire] prefab_unavailable prefab={prefabName} hash={prefabHash}");
            }

            return;
        }

        float currentFuel = zdo.GetFloat(ZDOVars.s_fuel, -1f);
        if (!RefillPolicy.ShouldRefill(currentFuel, fireplace.m_maxFuel))
        {
            return;
        }

        // The dedicated server owns the canonical ZDO database even when a
        // vanilla client owns and simulates the loaded scene object. Updating
        // the server copy after deserialization advances its data revision, so
        // Valheim sends the corrected native fuel field back to that client.
        // The low-water policy avoids revising the ZDO on every client tick.
        zdo.Set(ZDOVars.s_fuel, fireplace.m_maxFuel);
        Plugin.Log.LogInfo(
            $"[diag][EternalFire] refilled source={source} prefab={prefabName} " +
            $"from={currentFuel:0.###} to={fireplace.m_maxFuel:0.###} zdo={zdo.m_uid}");
    }
}

[HarmonyPatch(typeof(ZDO), nameof(ZDO.Load), new[] { typeof(ZPackage), typeof(int) })]
internal static class LoadedWorldFuelPatch
{
    private static void Postfix(ZDO __instance)
    {
        ZdoFuelNormalizer.Normalize(__instance, "world_load");
    }
}

[HarmonyPatch(typeof(ZDO), nameof(ZDO.LoadOldFormat), new[] { typeof(ZPackage), typeof(int) })]
internal static class LoadedLegacyWorldFuelPatch
{
    private static void Postfix(ZDO __instance)
    {
        ZdoFuelNormalizer.Normalize(__instance, "legacy_world_load");
    }
}

[HarmonyPatch(typeof(ZDO), nameof(ZDO.Deserialize), new[] { typeof(ZPackage) })]
internal static class ClientFuelUpdatePatch
{
    private static void Postfix(ZDO __instance)
    {
        ZdoFuelNormalizer.Normalize(__instance, "client_update");
    }
}
