using System;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Affinities;

[HarmonyPatch(typeof(InventoryGui), "Awake")]
internal static class AffinityForgeUiAwakePatch
{
    [HarmonyPostfix]
    private static void Postfix(InventoryGui __instance)
    {
        try
        {
            AffinityForgeUi.Attach(__instance);
        }
        catch (Exception exception)
        {
            AffinityForgeUi? controller = __instance.GetComponent<AffinityForgeUi>();
            if (controller != null) UnityEngine.Object.Destroy(controller);
            Transform? partialTab = __instance.m_tabUpgrade.transform.parent.Find("Benheim Affinity Tab");
            if (partialTab != null) UnityEngine.Object.Destroy(partialTab.gameObject);
            Plugin.Log.LogWarning(
                $"Affinity Forge tab is unavailable: {Diagnostics.Flatten(exception.Message)}");
            AffinityDiagnostics.Emit(
                DiagnosticEvent.Create("Affinity", "affinity_menu_unavailable")
                    .String("reason", Diagnostics.Flatten(exception.Message)));
        }
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateCraftingPanel")]
internal static class AffinityForgeUiCraftingPanelPatch
{
    [HarmonyPrefix]
    private static bool Prefix(InventoryGui __instance)
    {
        AffinityForgeUi? ui = AffinityForgeUi.Find(__instance);
        if (ui?.Active != true) return true;
        if (!AffinityApplication.IsAtBaseGameForge(Player.m_localPlayer))
        {
            ui.LeaveForNative();
            return true;
        }
        ui.Refresh();
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(InventoryGui __instance)
    {
        AffinityForgeUi? ui = AffinityForgeUi.Find(__instance);
        if (ui?.Active != true) ui?.UpdateAvailability();
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateRecipe")]
internal static class AffinityForgeUiRecipePatch
{
    [HarmonyPrefix]
    private static bool Prefix(InventoryGui __instance)
    {
        AffinityForgeUi? ui = AffinityForgeUi.Find(__instance);
        if (ui?.Active != true) return true;
        if (!AffinityApplication.IsAtBaseGameForge(Player.m_localPlayer))
        {
            ui.LeaveAndRebuildNative();
            return false;
        }
        ui.Render();
        return false;
    }
}

[HarmonyPatch(typeof(InventoryGui), "UpdateRecipeGamepadInput")]
internal static class AffinityForgeUiGamepadPatch
{
    [HarmonyPrefix]
    private static bool Prefix(InventoryGui __instance)
    {
        AffinityForgeUi? ui = AffinityForgeUi.Find(__instance);
        if (ui?.Active != true) return true;
        ui.HandleGamepadInput();
        return false;
    }
}

[HarmonyPatch(typeof(InventoryGui), "OnCraftPressed")]
internal static class AffinityForgeUiApplyPatch
{
    [HarmonyPrefix]
    private static bool Prefix(InventoryGui __instance)
    {
        AffinityForgeUi? ui = AffinityForgeUi.Find(__instance);
        if (ui?.Active != true) return true;
        ui.ConfirmApply();
        return false;
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTabCraftPressed))]
internal static class AffinityForgeUiCraftTabPatch
{
    [HarmonyPrefix]
    private static void Prefix(InventoryGui __instance)
    {
        AffinityForgeUi.Find(__instance)?.LeaveForNative();
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.OnTabUpgradePressed))]
internal static class AffinityForgeUiUpgradeTabPatch
{
    [HarmonyPrefix]
    private static void Prefix(InventoryGui __instance)
    {
        AffinityForgeUi.Find(__instance)?.LeaveForNative();
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Hide))]
internal static class AffinityForgeUiHidePatch
{
    [HarmonyPrefix]
    private static void Prefix(InventoryGui __instance)
    {
        AffinityForgeUi.Find(__instance)?.LeaveForNative();
    }
}

[HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
internal static class AffinityForgeUiShowPatch
{
    [HarmonyPostfix]
    private static void Postfix(InventoryGui __instance)
    {
        AffinityForgeUi.Find(__instance)?.UpdateAvailability();
    }
}
