using System;
using HarmonyLib;

namespace BenheimQoL.Farming;

/// <summary>
/// Keeps grid selection and native hotbar suppression on one ZInput update.
/// The methods are internal so the boundary harness executes the same entry
/// points Harmony invokes in the game.
/// </summary>
[HarmonyPatch]
internal static class FarmingInputPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ZInput), nameof(ZInput.Update))]
    internal static void ZInputUpdatePostfix(bool __runOriginal)
    {
        FarmingInputProbe.ObserveZInputPostfix(__runOriginal);
        if (!__runOriginal)
        {
            FarmingInputDiagnostics.ObserveSelection("native_update_skipped");
            return;
        }

        string decision = FarmingInput.UpdateGridSelection(Player.m_localPlayer);
        FarmingInputDiagnostics.ObserveSelection(decision);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    [HarmonyPatch(typeof(Player), "Update")]
    internal static void PlayerUpdatePrefix(Player __instance)
    {
        FarmingInput.BeginPlayerUpdate(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Player), "Update")]
    internal static void PlayerUpdatePostfix()
    {
        FarmingInput.EndPlayerUpdate();
    }

    [HarmonyFinalizer]
    [HarmonyPriority(Priority.First)]
    [HarmonyPatch(typeof(Player), "Update")]
    internal static Exception? PlayerUpdateFinalizer(Exception? __exception)
    {
        FarmingInput.EndPlayerUpdate();
        return __exception;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "UseHotbarItem")]
    internal static bool UseHotbarItemPrefix(Player __instance, int index)
    {
        bool suppressed = FarmingInput.ShouldSuppressHotbarUse(__instance, index);
        FarmingInputDiagnostics.ObserveHotbarUse(__instance, index, suppressed);
        FarmingInputProbe.ObserveHotbarUse(__instance, index, suppressed);
        return !suppressed;
    }
}
