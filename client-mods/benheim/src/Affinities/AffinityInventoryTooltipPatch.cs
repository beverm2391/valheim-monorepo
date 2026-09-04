using HarmonyLib;

namespace BenheimQoL.Affinities;

[HarmonyPatch(
    typeof(InventoryGrid),
    "CreateItemTooltip",
    new[] { typeof(ItemDrop.ItemData), typeof(UITooltip) })]
internal static class AffinityInventoryTooltipPatch
{
    [HarmonyPostfix]
    private static void Postfix(
        InventoryGrid __instance,
        ItemDrop.ItemData item,
        UITooltip tooltip)
    {
        AffinityLoadResult affinity = AffinityState.Read(item);
        if (affinity != AffinityLoadResult.Lunge
            && affinity != AffinityLoadResult.Snipe
            && affinity != AffinityLoadResult.Test)
        {
            return;
        }

        // Rebuild only this hovered item's native tooltip. SharedData belongs
        // to the prefab, so mutating its name or description would rename every
        // weapon instead of following the exact item that owns custom affinity data.
        tooltip.Set(
            AffinityPresentation.InventoryTitle(item.m_shared.m_name, affinity),
            AffinityPresentation.InventoryTooltip(
                item.GetTooltip(),
                affinity,
                LungeRuntime.Force,
                LungeRuntime.MinimumVerticalVelocity),
            __instance.m_tooltipAnchor);
    }
}
