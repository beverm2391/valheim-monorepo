using HarmonyLib;

namespace BenheimQoL.Interaction;

[HarmonyPatch(typeof(Feast), "Start")]
internal static class FeastInteractionRangePatch
{
    private static void Postfix(Feast __instance)
    {
        float previous = __instance.m_useDistance;
        __instance.m_useDistance = FeastInteractionRange.Resolve(previous);
    }
}
