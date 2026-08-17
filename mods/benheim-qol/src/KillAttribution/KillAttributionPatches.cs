using HarmonyLib;

namespace BenheimQoL.KillAttribution;

[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
internal static class KillAttributionPatches
{
    [HarmonyPrefix]
    private static void BeforeApplyDamage(
        Character __instance,
        HitData hit,
        out LethalHitObservation __state)
    {
        __state = LethalHitObservation.Capture(__instance, hit);
    }

    [HarmonyPostfix]
    private static void AfterApplyDamage(
        Character __instance,
        LethalHitObservation __state)
    {
        if (__state.BecameLethal(__instance))
        {
            KillAttributionClient.Report(__state);
        }
    }
}
