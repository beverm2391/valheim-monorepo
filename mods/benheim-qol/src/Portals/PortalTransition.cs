using System.Reflection;
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
            return;
        }

        TeleportTimerField.SetValue(player, Mathf.Max(0f, VanillaDistantPortalDelay - TargetMinimumDelay));
    }
}
