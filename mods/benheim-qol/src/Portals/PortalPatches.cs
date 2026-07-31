using HarmonyLib;

namespace BenheimQoL.Portals;

[HarmonyPatch(typeof(TextInput), "Update")]
internal static class PortalAutocompletePatch
{
    private static void Postfix(TextInput __instance)
    {
        PortalAutocomplete.CycleMatch(__instance);
    }
}

[HarmonyPatch(typeof(TeleportWorld), "GetText")]
internal static class RememberReadPortalTagPatch
{
    private static void Postfix(string __result)
    {
        PortalAutocomplete.RememberTag(__result);
    }
}

[HarmonyPatch(typeof(TeleportWorld), "SetText")]
internal static class RememberWrittenPortalTagPatch
{
    private static void Prefix(string text)
    {
        PortalAutocomplete.RememberTag(text);
    }
}

[HarmonyPatch(typeof(Player), "TeleportTo")]
internal static class FasterPortalPatch
{
    private static void Postfix(bool __result, Player __instance, bool distantTeleport)
    {
        PortalTransition.ShortenMinimumDelay(__instance, __result, distantTeleport);
    }
}
