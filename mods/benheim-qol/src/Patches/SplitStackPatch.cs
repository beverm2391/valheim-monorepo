using System;
using System.Reflection;
using HarmonyLib;

namespace BenheimQoL.Patches;

[HarmonyPatch(typeof(InventoryGui), "ShowSplitDialog")]
internal static class SplitStackPatch
{
    private static readonly FieldInfo SplitInputField =
        AccessTools.Field(typeof(InventoryGui), "m_splitInput");

    private static readonly FieldInfo LastSplitInputField =
        AccessTools.Field(typeof(InventoryGui), "m_lastSplitInput");

    private static readonly FieldInfo SplitNumInputTimeoutSecField =
        AccessTools.Field(typeof(InventoryGui), "m_splitNumInputTimeoutSec");

    private static void Postfix(InventoryGui __instance)
    {
        try
        {
            SplitInputField.SetValue(__instance, string.Empty);
            LastSplitInputField.SetValue(__instance, DateTime.MinValue);
            SplitNumInputTimeoutSecField.SetValue(__instance, 10f);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not prime split-stack numeric input: {ex.Message}");
        }
    }
}
