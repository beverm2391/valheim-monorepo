using System;
using HarmonyLib;

namespace BenheimQoL.Shortcuts;

[HarmonyPatch(typeof(Player), "TakeInput", new Type[] { })]
internal static class ShortcutOverlayPlayerInputPatch
{
    private static bool Prefix(ref bool __result)
    {
        if (!ShortcutOverlay.IsOpen)
        {
            return true;
        }

        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(Menu), "IsVisible", new Type[] { })]
internal static class ShortcutOverlayMenuVisibilityPatch
{
    private static void Postfix(ref bool __result)
    {
        __result = __result || ShortcutOverlay.IsOpen;
    }
}

[HarmonyPatch(typeof(Menu), "Update", new Type[] { })]
internal static class ShortcutOverlayMenuUpdatePatch
{
    private static bool Prefix()
    {
        return !ShortcutOverlay.IsOpen;
    }
}
