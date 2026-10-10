using HarmonyLib;
using Benheim.Resident;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.GreydwarfResident;

// Native Chair.Interact already rejects IsInUse. Extend that exact decision
// for a cosmetic occupant instead of fabricating a Player or adding physics.
[HarmonyPatch(typeof(Chair), nameof(Chair.IsInUse))]
internal static class ResidentSeatInUsePatch
{
    private static void Postfix(Chair __instance, ref bool __result)
    {
        if (GreydwarfResidentRuntime.IsReserved(__instance))
        {
            __result = true;
        }
    }
}

[HarmonyPatch(typeof(Chair), nameof(Chair.GetHoverText))]
internal static class ResidentSeatHoverPatch
{
    private static void Postfix(Chair __instance, ref string __result)
    {
        if (InputState.IsShiftHeld() && ResidentTub.TryGet(__instance.gameObject, out ZNetView view))
        {
            __result = "George\n[<color=yellow><b>$KEY_Use</b></color>] " +
                (ResidentTub.IsInvited(view.GetZDO()) ? "Dismiss George" : "Invite George");
            __result = Localization.instance.Localize(__result);
            return;
        }
        if (GreydwarfResidentRuntime.IsReserved(__instance)) __result = string.Empty;
    }
}

[HarmonyPatch(typeof(Chair), nameof(Chair.Interact))]
internal static class ResidentSeatInteractPatch
{
    private static bool Prefix(Chair __instance, Humanoid user, bool hold, ref bool __result)
    {
        if (hold || user != Player.m_localPlayer || !InputState.IsShiftHeld() ||
            !ResidentTub.TryGet(__instance.gameObject, out ZNetView view)) return true;
        ResidentClient.Place(view);
        __result = true;
        return false;
    }
}
