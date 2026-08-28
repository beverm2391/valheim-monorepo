using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

[HarmonyPatch]
internal static class WildernessDangerPresentationPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "UpdateBiome")]
    private static void UpdateBiomePostfix(Minimap __instance)
    {
        WildernessDangerPresentation.RefreshMinimap(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MessageHud), "UpdateBiomeFound")]
    private static void UpdateBiomeFoundPostfix(GameObject ___m_biomeMsgInstance)
    {
        WildernessDangerPresentation.FitArrivalBanner(___m_biomeMsgInstance);
    }
}
