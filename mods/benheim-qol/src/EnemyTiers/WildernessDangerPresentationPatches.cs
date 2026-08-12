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
        WildernessMinimapIndicator.Update(
            __instance,
            WildernessDangerPresentation.CurrentDanger);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(MessageHud), "UpdateBiomeFound")]
    private static void UpdateBiomeFoundPostfix(GameObject ___m_biomeMsgInstance)
    {
        WildernessDangerPresentation.FitArrivalBanner(___m_biomeMsgInstance);
    }
}
