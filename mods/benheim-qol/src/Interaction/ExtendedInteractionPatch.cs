using HarmonyLib;

namespace BenheimQoL.Interaction;

[HarmonyPatch(typeof(Player), "Awake")]
internal static class ExtendedInteractionPatch
{
    private const float ExtendedInteractDistance = 8f;

    private static void Postfix(Player __instance)
    {
        if (__instance.m_maxInteractDistance < ExtendedInteractDistance)
        {
            __instance.m_maxInteractDistance = ExtendedInteractDistance;
        }
    }
}
