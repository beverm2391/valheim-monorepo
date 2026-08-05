using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackPatches
{
    [HarmonyPatch(typeof(Container), "RPC_StackResponse")]
    private static class StackResponsePatch
    {
        private static bool Prefix(Container __instance, long uid, bool granted)
        {
            return QuickStack.BeginNativeStackResponse(__instance, granted);
        }

        private static void Postfix(Container __instance)
        {
            QuickStack.CompleteNativeStackResponse(__instance);
        }
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
