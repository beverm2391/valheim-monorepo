using HarmonyLib;

namespace BenheimQoL.Woodcutting;

[HarmonyPatch]
internal static class WoodcuttingPatches
{
    [HarmonyPatch(typeof(TreeBase), nameof(TreeBase.Damage))]
    private static class StandingTreeDamagePatch
    {
        private static void Postfix(TreeBase __instance, HitData hit)
        {
            WoodcuttingProgression.TryApplyCleave(__instance, hit);
        }
    }

    [HarmonyPatch(typeof(TreeLog), nameof(TreeLog.Damage))]
    private static class FallenLogDamagePatch
    {
        private static void Postfix(TreeLog __instance, HitData hit)
        {
            WoodcuttingProgression.TryApplyCleave(__instance, hit);
        }
    }
}
