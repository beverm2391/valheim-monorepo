using HarmonyLib;

namespace BenheimQoL.Production;

[HarmonyPatch]
internal static class StationFillPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Smelter), "OnAddOre")]
    private static bool SmelterOrePrefix(
        Smelter __instance,
        Switch sw,
        Humanoid user,
        ItemDrop.ItemData? item,
        ref bool __result)
    {
        if (StationFill.IsInvokingVanilla)
        {
            return true;
        }

        if (!StationFill.TryStartSmelterOre(__instance, sw, user, item))
        {
            return true;
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
    private static bool SmelterFuelPrefix(
        Smelter __instance,
        Switch sw,
        Humanoid user,
        ItemDrop.ItemData? item,
        ref bool __result)
    {
        if (StationFill.IsInvokingVanilla)
        {
            return true;
        }

        if (!StationFill.TryStartSmelterFuel(__instance, sw, user, item))
        {
            return true;
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CookingStation), "OnAddFoodSwitch")]
    private static bool CookingFoodPrefix(
        CookingStation __instance,
        Switch caller,
        Humanoid user,
        ItemDrop.ItemData? item,
        ref bool __result)
    {
        if (StationFill.IsInvokingVanilla)
        {
            return true;
        }

        if (!StationFill.TryStartCookingFood(__instance, caller, user, item))
        {
            return true;
        }

        __result = true;
        return false;
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")]
    private static bool CookingFuelPrefix(
        CookingStation __instance,
        Switch sw,
        Humanoid user,
        ItemDrop.ItemData? item,
        ref bool __result)
    {
        if (StationFill.IsInvokingVanilla)
        {
            return true;
        }

        if (!StationFill.TryStartCookingFuel(__instance, sw, user, item))
        {
            return true;
        }

        __result = true;
        return false;
    }
}
