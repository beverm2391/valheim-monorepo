using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackPatches
{
    [HarmonyPatch(typeof(Container), "RPC_OpenRespons")]
    private static class OpenResponsePatch
    {
        private static void Postfix(Container __instance, bool granted)
        {
            if (granted)
            {
                // Capture exactly the cached inventory that InventoryGui.Show just
                // presented. Waiting for CheckForChanges would hide a stale first
                // open, which is the cross-client regression this probe must expose.
                QuickStackDiagnostics.ContainerOpened(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
    private static class StackResponsePatch
    {
        private static bool Prefix(Container __instance, bool granted)
        {
            if (QuickStack.TryHandleTimedOutResponse(__instance, granted))
            {
                return false;
            }

            return granted || !QuickStack.TryHandleNativeDenial(__instance);
        }
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll), new[] { typeof(Inventory), typeof(bool) })]
    private static class BulkStackPatch
    {
        private static bool Prefix(Inventory __instance, Inventory fromInventory, out QuickStackBulkScope? __state)
        {
            __state = QuickStack.BeginBulkStack(__instance, fromInventory);
            return QuickStack.ShouldRunBulkStack(__state);
        }

        private static void Postfix(QuickStackBulkScope? __state) => QuickStack.CompleteBulkStack(__state);

        private static System.Exception? Finalizer(QuickStackBulkScope? __state, System.Exception? __exception) =>
            QuickStack.FinalizeBulkStack(__state, __exception);
    }

    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })]
    private static class FilterProtectedItemsPatch
    {
        private static bool Prefix(Inventory __instance, ItemDrop.ItemData item)
        {
            return QuickStack.ShouldAllowNativeAdd(__instance, item);
        }
    }

    [HarmonyPatch(typeof(Player), "Message", new[]
    {
        typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(Sprite),
    })]
    private static class SuppressNativeStackMessagePatch
    {
        private static bool Prefix(MessageHud.MessageType type, string msg)
        {
            return !QuickStack.ShouldSuppressNativeStackMessage(type, msg);
        }
    }
}
