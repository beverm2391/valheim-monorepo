using System;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

internal static class HoeRadiusPreview
{
    private static GameObject? trackedGhost;
    private static Transform? trackedVisual;
    private static Vector3 nativeScale;
    private static string lastState = string.Empty;

    internal static void Update(Player player)
    {
        try
        {
            GameObject? ghost = FarmingReflection.PlacementGhostField.GetValue(player) as GameObject;
            Track(ghost);
            bool hoeAction = ghost != null && HoeRadiusExpansion.IsSelectedHoeTerrainAction(player);
            bool active = hoeAction && InputState.IsLeftShiftHeld();

            if (trackedVisual == null)
            {
                if (active)
                {
                    State("failed", "ghost_visual_missing", ghost);
                }
                return;
            }

            Vector3 desired = active ? nativeScale * HoeRadiusExpansion.RadiusMultiplier : nativeScale;
            if (Vector3.Distance(trackedVisual.localScale, desired) > 0.001f)
            {
                trackedVisual.localScale = desired;
            }

            if (!hoeAction)
            {
                lastState = string.Empty;
                return;
            }

            State(active ? "expanded" : "native", active ? "left_shift" : "modifier_released", ghost);
        }
        catch (Exception exception)
        {
            State("failed", exception.GetType().Name, trackedGhost);
        }
    }

    internal static void Reset()
    {
        if (trackedVisual != null)
        {
            trackedVisual.localScale = nativeScale;
        }
        trackedGhost = null;
        trackedVisual = null;
        nativeScale = Vector3.zero;
        lastState = string.Empty;
    }

    private static void Track(GameObject? ghost)
    {
        if (ghost == trackedGhost)
        {
            return;
        }

        if (trackedVisual != null)
        {
            trackedVisual.localScale = nativeScale;
        }

        trackedGhost = ghost;
        trackedVisual = ghost?.transform.Find("_GhostOnly");
        nativeScale = trackedVisual?.localScale ?? Vector3.zero;
        lastState = string.Empty;
    }

    private static void State(string result, string reason, GameObject? ghost)
    {
        string state = result + ":" + reason + ":" + (ghost?.name ?? string.Empty);
        if (state == lastState)
        {
            return;
        }

        lastState = state;
        Emit(DiagnosticEvent.Create("Farming", "hoe_radius_preview")
            .String("result", result)
            .String("reason", reason)
            .String("ghost", ghost?.name)
            .Boolean("visual_found", trackedVisual != null)
            .Number("radius_multiplier", result == "expanded" ? HoeRadiusExpansion.RadiusMultiplier : 1f));
    }

    private static void Emit(DiagnosticEvent record)
    {
        try { Diagnostics.Emit(record); }
        catch (Exception) { }
    }
}
