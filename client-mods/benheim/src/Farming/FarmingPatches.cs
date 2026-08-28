using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Farming;

[HarmonyPatch]
internal static class FarmingPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "Interact")]
    private static void InteractPrefix(
        Player __instance,
        GameObject go,
        bool hold,
        bool alt,
        out MassHarvestResult? __state)
    {
        __state = MassHarvest.Begin(__instance, go, hold, alt);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "Interact")]
    private static void InteractPostfix(MassHarvestResult? __state)
    {
        MassHarvest.Complete(__state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "TryPlacePiece")]
    private static void TryPlacePiecePrefix(int ___m_placeRotation)
    {
        if (FarmingInput.IsMassActionHeld() && PlantingState.Rotation is null)
        {
            PlantingState.Rotation = ___m_placeRotation;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "TryPlacePiece")]
    private static void TryPlacePiecePostfix(
        Player __instance,
        bool __result,
        Piece piece,
        ref int ___m_placeRotation)
    {
        if (__result)
        {
            PlantingState.CaptureAnchor(__instance, piece);
        }

        if (FarmingInput.IsMassActionHeld() && PlantingState.Rotation.HasValue)
        {
            ___m_placeRotation = PlantingState.Rotation.Value;
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    private static void UpdatePlacementPrefix(ref int ___m_placeRotation)
    {
        PlantingState.AnchorPlaced = false;
        if (FarmingInput.IsMassActionHeld() && PlantingState.Rotation.HasValue)
        {
            ___m_placeRotation = PlantingState.Rotation.Value;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "UpdatePlacement")]
    private static void UpdatePlacementPostfix(Player __instance, int ___m_placeRotation)
    {
        if (FarmingInput.IsMassActionHeld())
        {
            PlantingState.Rotation = ___m_placeRotation;
        }

        MassPlanting.TryPlantGrid(__instance);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Player), "SetupPlacementGhost")]
    private static void SetupPlacementGhostPrefix(int ___m_placeRotation)
    {
        if (FarmingInput.IsMassActionHeld() && PlantingState.Rotation is null)
        {
            PlantingState.Rotation = ___m_placeRotation;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "SetupPlacementGhost")]
    private static void SetupPlacementGhostPostfix(ref int ___m_placeRotation)
    {
        if (FarmingInput.IsMassActionHeld() && PlantingState.Rotation.HasValue)
        {
            ___m_placeRotation = PlantingState.Rotation.Value;
        }

        PlantingPreview.DestroyGhosts();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    private static void UpdatePlacementGhostPostfix(Player __instance)
    {
        PlantingPreview.Update(__instance);
    }
}
