using HarmonyLib;

namespace BenheimQoL.Production;

[HarmonyPatch]
internal static class StoneOvenPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(CookingStation), "Awake")]
    private static void CookingStationAwakePostfix(CookingStation __instance)
    {
        StoneOven.ApplyBakeTime(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CookingStation), "UpdateCooking")]
    private static void CookingStationUpdatePostfix(CookingStation __instance)
    {
        StoneOven.ObserveNativeOwner(__instance);
    }
}
