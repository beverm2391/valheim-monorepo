using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Portals;

internal static class PortalTransition
{
    private const float VanillaDistantPortalDelay = 8f;
    private const float TargetMinimumDelay = 1.25f;

    private static readonly FieldInfo TeleportTimerField =
        AccessTools.Field(typeof(Player), "m_teleportTimer");

    internal static void ShortenMinimumDelay(Player player, bool teleportStarted, bool distantTeleport)
    {
        if (!teleportStarted || !distantTeleport)
        {
            Diagnostics.Event(
                "Portals",
                "transition_unchanged",
                $"started={Diagnostics.Bool(teleportStarted)} distant={Diagnostics.Bool(distantTeleport)}");
            return;
        }

        float previous = (float)TeleportTimerField.GetValue(player);
        TeleportTimerField.SetValue(player, Mathf.Max(0f, VanillaDistantPortalDelay - TargetMinimumDelay));
        float current = (float)TeleportTimerField.GetValue(player);
        Diagnostics.Event(
            "Portals",
            "transition_shortened",
            $"previous_timer={previous:0.##} current_timer={current:0.##} target_minimum={TargetMinimumDelay:0.##}");
    }
}
