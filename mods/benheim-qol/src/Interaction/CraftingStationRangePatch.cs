using HarmonyLib;

namespace BenheimQoL.Interaction;

[HarmonyPatch(typeof(CraftingStation), "Start")]
internal static class CraftingStationRangePatch
{
    private const float ExtendedUseDistance = 8f;

    private static void Postfix(CraftingStation __instance)
    {
        if (__instance.m_useDistance < ExtendedUseDistance)
        {
            __instance.m_useDistance = ExtendedUseDistance;
        }
    }
}
