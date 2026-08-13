using BenheimQoL.EnemyTiers;
using BenheimQoL.Infrastructure;
using HarmonyLib;

namespace BenheimTestCommands;

[HarmonyPatch]
internal static class BoarTierIdentityPatches
{
    [HarmonyPatch(typeof(LevelEffects), "Start")]
    [HarmonyPostfix]
    private static void AfterLevelEffectsStart(LevelEffects __instance)
    {
        Emit(BoarTierIdentity.Apply(__instance, "server_level_effects_start"));
    }

    [HarmonyPatch(typeof(LevelEffects), "OnLevelSet")]
    [HarmonyPostfix]
    private static void AfterLevelSet(LevelEffects __instance)
    {
        Emit(BoarTierIdentity.Apply(__instance, "server_level_changed"));
    }

    private static void Emit(DiagnosticEvent? diagnosticEvent)
    {
        if (diagnosticEvent != null)
        {
            ServerDiagnostics.Emit(diagnosticEvent);
        }
    }
}
