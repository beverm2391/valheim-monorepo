using HarmonyLib;

namespace BenheimQoL.Affinities;

[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
internal static class LungeAttackStartPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        Humanoid __instance,
        bool secondaryAttack,
        bool __result,
        Attack? ___m_currentAttack)
    {
        if (__result && ___m_currentAttack != null)
        {
            LungeRuntime.ObserveAttackStarted(__instance, ___m_currentAttack, secondaryAttack);
        }
    }
}

[HarmonyPatch(typeof(Attack), "DoMeleeAttack")]
internal static class LungeMeleeEventPatch
{
    [HarmonyPrefix]
    private static void Prefix(Attack __instance, Humanoid ___m_character)
    {
        LungeRuntime.Consume(__instance, ___m_character);
    }
}
