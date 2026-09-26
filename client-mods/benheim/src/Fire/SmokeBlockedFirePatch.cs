using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.Fire;

// Fireplace.CheckUnderTerrain calls this only after its terrain and cover
// checks pass. Replacing the smoke-block result leaves those checks and the
// native smoke spawner (including smoke damage) untouched.
[HarmonyPatch(typeof(SmokeSpawner), nameof(SmokeSpawner.IsBlocked))]
internal static class SmokeBlockedFirePatch
{
    private static bool reportedOverride;

    [HarmonyPostfix]
    private static void Postfix(SmokeSpawner __instance, ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        Fireplace? fireplace = __instance.GetComponentInParent<Fireplace>();
        if (fireplace == null || fireplace.m_smokeSpawner != __instance)
        {
            return;
        }

        __result = false;
        if (reportedOverride)
        {
            return;
        }

        reportedOverride = true;
        try
        {
            Diagnostics.Emit(DiagnosticEvent.Create("Fire", "smoke_block_override")
                .String("fireplace", fireplace.gameObject.name));
        }
        catch (Exception)
        {
            // Evidence capture must not restore the smoke shutoff.
        }
    }
}
