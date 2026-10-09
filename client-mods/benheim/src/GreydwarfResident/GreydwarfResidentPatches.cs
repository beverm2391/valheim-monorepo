using HarmonyLib;

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
            ResidentDiagnostics.Emit("seat_blocked", "resident_occupant");
        }
    }
}

[HarmonyPatch(typeof(Chair), nameof(Chair.GetHoverText))]
internal static class ResidentSeatHoverPatch
{
    private static void Postfix(Chair __instance, ref string __result)
    {
        if (GreydwarfResidentRuntime.IsReserved(__instance)) __result = string.Empty;
    }
}
