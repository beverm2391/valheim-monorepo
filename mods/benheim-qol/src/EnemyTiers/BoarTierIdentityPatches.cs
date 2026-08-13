using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimQoL.EnemyTiers;

[HarmonyPatch]
internal static class BoarTierIdentityPatches
{
    [HarmonyPatch(typeof(LevelEffects), "Start")]
    [HarmonyPostfix]
    private static void AfterLevelEffectsStart(LevelEffects __instance)
    {
        Emit(BoarTierIdentity.Apply(__instance, "level_effects_start"));
    }

    [HarmonyPatch(typeof(LevelEffects), "OnLevelSet")]
    [HarmonyPostfix]
    private static void AfterLevelSet(LevelEffects __instance)
    {
        Emit(BoarTierIdentity.Apply(__instance, "level_changed"));
    }

    private static void Emit(DiagnosticEvent? diagnosticEvent)
    {
        if (diagnosticEvent != null)
        {
            Diagnostics.Emit(diagnosticEvent);
        }
    }
}
