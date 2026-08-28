using HarmonyLib;

namespace BenheimQoL.Production;

[HarmonyPatch]
internal static class CookingDiagnosticPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(CookingStation), "CookItem")]
    private static void CookItemPrefix(
        CookingStation __instance,
        Humanoid user,
        ItemDrop.ItemData item,
        out CookingDiagnostics.RequestObservation __state)
    {
        __state = CookingDiagnostics.BeginRequest(__instance, user, item);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CookingStation), "CookItem")]
    private static void CookItemPostfix(
        CookingStation __instance,
        Humanoid user,
        bool __result,
        CookingDiagnostics.RequestObservation __state)
    {
        CookingDiagnostics.FinishRequest(__instance, user, __result, __state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CookingStation), "RPC_AddItem")]
    private static void AddItemPrefix(
        CookingStation __instance,
        long sender,
        string itemName,
        out CookingDiagnostics.OwnerObservation __state)
    {
        __state = CookingDiagnostics.BeginOwnerDecision(__instance, sender, itemName);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CookingStation), "RPC_AddItem")]
    private static void AddItemPostfix(
        CookingStation __instance,
        CookingDiagnostics.OwnerObservation __state)
    {
        CookingDiagnostics.FinishOwnerDecision(__instance, __state);
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CookingStation), "UpdateCooking")]
    private static void UpdateCookingPrefix(
        CookingStation __instance,
        out CookingDiagnostics.SlotObservation[] __state)
    {
        __state = CookingDiagnostics.Snapshot(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CookingStation), "UpdateCooking")]
    private static void UpdateCookingPostfix(
        CookingStation __instance,
        CookingDiagnostics.SlotObservation[] __state)
    {
        CookingDiagnostics.ReportTransitions(__instance, __state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(CookingStation), "SpawnItem")]
    private static void SpawnItemPostfix(CookingStation __instance, string name, int slot)
    {
        CookingDiagnostics.OutputSpawned(__instance, name, slot);
    }
}
