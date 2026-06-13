using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Patches;

[HarmonyPatch(typeof(Player), "TeleportTo")]
internal static class FasterPortalPatch
{
    private const float VanillaDistantPortalDelay = 8f;
    private const float TargetMinimumDelay = 1.25f;

    private static readonly FieldInfo TeleportTimerField =
        AccessTools.Field(typeof(Player), "m_teleportTimer");

    private static void Postfix(bool __result, Player __instance, bool distantTeleport)
    {
        if (!__result || !distantTeleport)
        {
            return;
        }

        TeleportTimerField.SetValue(__instance, Mathf.Max(0f, VanillaDistantPortalDelay - TargetMinimumDelay));
    }
}
