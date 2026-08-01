using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Interaction;

[HarmonyPatch(typeof(CraftingStation), "Start")]
internal static class CraftingStationRangePatch
{
    private static void Postfix(CraftingStation __instance)
    {
        float previous = __instance.m_useDistance;
        if (__instance.m_useDistance < InteractionRanges.UseDistance)
        {
            __instance.m_useDistance = InteractionRanges.UseDistance;
        }

        Diagnostics.Event(
            "Interaction",
            "station_range_ready",
            $"station=\"{__instance.gameObject.name}\" previous={previous:0.##} current={__instance.m_useDistance:0.##}");
    }
}
