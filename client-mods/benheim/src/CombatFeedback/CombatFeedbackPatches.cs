using HarmonyLib;

namespace BenheimQoL.CombatFeedback;

[HarmonyPatch(typeof(GameCamera), "LateUpdate")]
internal static class BowFocusCameraPatch
{
    [HarmonyPostfix]
    private static void Postfix(GameCamera __instance)
    {
        CombatFeedbackController.UpdateBowFocus(__instance);
    }
}
