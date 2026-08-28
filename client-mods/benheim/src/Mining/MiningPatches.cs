using HarmonyLib;

namespace BenheimQoL.Mining;

[HarmonyPatch]
internal static class MiningPatches
{
    [HarmonyPatch(typeof(MineRock), "Damage")]
    private static class MineRockDamagePatch
    {
        private static void Prefix(HitData hit)
        {
            MiningProgression.EnhancePrimaryHit(hit);
        }

        private static void Postfix(MineRock __instance, HitData hit)
        {
            MiningProgression.TryApplyAoe(__instance, hit);
        }
    }

    [HarmonyPatch(typeof(MineRock5), "Damage")]
    private static class MineRock5DamagePatch
    {
        private static void Prefix(HitData hit)
        {
            MiningProgression.EnhancePrimaryHit(hit);
        }

        private static void Postfix(MineRock5 __instance, HitData hit)
        {
            MiningProgression.TryApplyAoe(__instance, hit);
        }
    }
}
