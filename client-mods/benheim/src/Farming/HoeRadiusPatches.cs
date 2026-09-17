using HarmonyLib;

namespace BenheimQoL.Farming;

[HarmonyPatch]
internal static class HoeRadiusPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(TerrainOp), "Awake")]
    private static void TerrainOpAwakePrefix(TerrainOp __instance)
    {
        HoeRadiusExpansion.TryPrepareOperation(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TerrainOp.Settings), nameof(TerrainOp.Settings.Serialize))]
    private static void SettingsSerializePostfix(TerrainOp.Settings __instance, ZPackage pkg)
    {
        HoeRadiusExpansion.TryWriteProtocol(__instance, pkg);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TerrainOp.Settings), nameof(TerrainOp.Settings.Deserialize))]
    private static void SettingsDeserializePostfix(ZPackage pkg, ref TerrainOp.Settings __result)
    {
        TerrainOp.Settings? settings = __result;
        HoeRadiusExpansion.TryReadProtocol(pkg, ref settings);
        __result = settings!;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(TerrainComp), "DoOperation")]
    private static void TerrainOperationPostfix(TerrainOp.Settings modifier)
    {
        HoeRadiusExpansion.RecordApplied(modifier);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
    private static void PlacementGhostPostfix(Player __instance)
    {
        if (__instance == Player.m_localPlayer)
        {
            HoeRadiusPreview.Update(__instance);
        }
    }
}
