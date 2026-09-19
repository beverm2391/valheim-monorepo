using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Interaction;

// AutoPickup reads this field for both its physics query and its final distance
// check. Changing it after native player setup leaves all of Valheim's pickup
// gates, movement, and the player's auto-pickup toggle in their native path.
[HarmonyPatch(typeof(Player), "Awake")]
internal static class AutoPickupRangePatch
{
    internal const float Multiplier = 2f;

    [HarmonyPostfix]
    private static void Postfix(Player __instance)
    {
        float nativeRange = __instance.m_autoPickupRange;
        float extendedRange = nativeRange * Multiplier;
        __instance.m_autoPickupRange = extendedRange;

        // Player.Awake also runs for remote player objects. The field is local
        // state, while native AutoPickup runs only for the local player.
        try
        {
            Diagnostics.Emit(DiagnosticEvent.Create("Interaction", "auto_pickup_range_applied")
                .Number("native_range", nativeRange)
                .Number("effective_range", extendedRange)
                .Number("multiplier", Multiplier));
        }
        catch (Exception)
        {
            // Evidence capture must not interrupt native player initialization.
        }
    }
}
