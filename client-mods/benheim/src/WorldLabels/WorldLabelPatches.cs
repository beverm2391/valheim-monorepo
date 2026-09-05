using HarmonyLib;

namespace BenheimQoL.WorldLabels;

[HarmonyPatch(typeof(Sign), "Awake")]
internal static class SignGlowAwakePatch
{
    private static void Postfix(Sign __instance)
    {
        WorldLabelRuntime.Attach(__instance);
    }
}

[HarmonyPatch(typeof(TeleportWorld), "Awake")]
internal static class PortalLabelAwakePatch
{
    private static void Postfix(TeleportWorld __instance)
    {
        WorldLabelRuntime.Attach(__instance);
    }
}
