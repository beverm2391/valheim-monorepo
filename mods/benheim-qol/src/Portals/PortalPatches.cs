using HarmonyLib;

namespace BenheimQoL.Portals;

[HarmonyPatch(typeof(Player), "TeleportTo")]
internal static class FasterPortalPatch
{
    private static void Postfix(bool __result, Player __instance, bool distantTeleport)
    {
        PortalTransition.ShortenMinimumDelay(__instance, __result, distantTeleport);
    }
}
