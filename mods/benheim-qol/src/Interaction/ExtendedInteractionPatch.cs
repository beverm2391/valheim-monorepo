using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Interaction;

[HarmonyPatch(typeof(Player), "Awake")]
internal static class ExtendedInteractionPatch
{
    private static void Postfix(Player __instance)
    {
        float previous = __instance.m_maxInteractDistance;
        if (__instance.m_maxInteractDistance < InteractionRanges.UseDistance)
        {
            __instance.m_maxInteractDistance = InteractionRanges.UseDistance;
        }

        if (__instance == Player.m_localPlayer || Player.m_localPlayer == null)
        {
            Diagnostics.Event(
                "Interaction",
                "player_range_ready",
                $"previous={previous:0.##} current={__instance.m_maxInteractDistance:0.##}");
        }
    }
}

[HarmonyPatch(typeof(InventoryGui), "Awake")]
internal static class ContainerInteractionRangePatch
{
    private static void Postfix(InventoryGui __instance)
    {
        float previous = __instance.m_autoCloseDistance;
        if (__instance.m_autoCloseDistance < InteractionRanges.ContainerAutoCloseDistance)
        {
            __instance.m_autoCloseDistance = InteractionRanges.ContainerAutoCloseDistance;
        }

        Diagnostics.Event(
            "Interaction",
            "container_auto_close_range_ready",
            $"previous={previous:0.##} current={__instance.m_autoCloseDistance:0.##}");
    }
}

[HarmonyPatch(typeof(Container), nameof(Container.Interact), new[] { typeof(Humanoid), typeof(bool), typeof(bool) })]
internal static class ContainerInteractionDiagnosticsPatch
{
    private static void Postfix(Container __instance, Humanoid character, bool hold, bool alt, bool __result)
    {
        if (character != Player.m_localPlayer)
        {
            return;
        }

        float distance = Vector3.Distance(__instance.transform.position, character.transform.position);
        Diagnostics.Event(
            "Interaction",
            "container_interact",
            $"container=\"{__instance.gameObject.name}\" distance={distance:0.##} hold={Diagnostics.Bool(hold)} alt={Diagnostics.Bool(alt)} accepted={Diagnostics.Bool(__result)}");
    }
}

[HarmonyPatch(typeof(Container), "RPC_OpenRespons")]
internal static class ContainerOpenResponseDiagnosticsPatch
{
    private static void Prefix(Container __instance, bool granted)
    {
        Player player = Player.m_localPlayer;
        float distance = player
            ? Vector3.Distance(__instance.transform.position, player.transform.position)
            : -1f;
        float autoCloseDistance = InventoryGui.instance ? InventoryGui.instance.m_autoCloseDistance : -1f;
        Diagnostics.Event(
            "Interaction",
            "container_open_response",
            $"container=\"{__instance.gameObject.name}\" granted={Diagnostics.Bool(granted)} distance={distance:0.##} auto_close_distance={autoCloseDistance:0.##}");
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show), new[] { typeof(Container), typeof(int) })]
internal static class ContainerGuiDiagnosticsPatch
{
    private static void Postfix(Container container)
    {
        Diagnostics.Event(
            "Interaction",
            "container_gui_shown",
            $"container=\"{(container ? container.gameObject.name : "none")}\"");
    }
}
