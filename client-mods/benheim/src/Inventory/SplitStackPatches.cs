using System;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

[HarmonyPatch(typeof(InventoryGui), "ShowSplitDialog")]
internal static class SplitStackPatches
{
    private static void Postfix(InventoryGui __instance)
    {
        try
        {
            SplitStackController.PrimeNumericInput(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not prime split-stack numeric input: {ex.Message}");
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "UpdateSplitDialog")]
    private static class DeleteClearsSplitAmountPatch
    {
        private static void Postfix(InventoryGui __instance)
        {
            // The split dialog is the text-entry surface, so these keys edit it.
            if (!__instance.m_splitSlider.gameObject.activeInHierarchy
                || (!Input.GetKeyDown(KeyCode.Backspace) && !Input.GetKeyDown(KeyCode.Delete)))
            {
                return;
            }

            SplitStackController.ClearAmount(__instance);
        }
    }

    [HarmonyPatch(typeof(InventoryGui), "OnSplitOk")]
    private static class AutoMoveSplitPatch
    {
        private static bool Prefix(InventoryGui __instance)
        {
            try
            {
                return !SplitStackController.TryAutoMove(__instance);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Split auto-transfer failed; falling back to vanilla split: {ex.Message}");
                return true;
            }
        }
    }
}
