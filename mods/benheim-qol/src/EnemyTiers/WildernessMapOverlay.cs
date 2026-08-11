using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.EnemyTiers;

internal static class WildernessMapOverlay
{
    private const string OverlayName = "BenheimWildernessPressure";

    private static Minimap? activeMap;
    private static RawImage? overlayImage;
    private static Texture2D? overlayTexture;
    private static bool explorationMaskDirty;
    private static bool lastShowSharedMapData;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "Update")]
    private static void MinimapUpdatePostfix(
        Minimap __instance,
        bool[] ___m_explored,
        bool[] ___m_exploredOthers,
        bool ___m_showSharedMapData)
    {
        if (__instance.m_mode != Minimap.MapMode.Large || WorldGenerator.instance == null)
        {
            return;
        }

        if (activeMap != __instance || overlayImage == null || overlayTexture == null)
        {
            Build(__instance, ___m_explored, ___m_exploredOthers, ___m_showSharedMapData);
        }

        if (overlayImage == null || overlayTexture == null)
        {
            return;
        }

        overlayImage.uvRect = __instance.m_mapImageLarge.uvRect;
        if (lastShowSharedMapData != ___m_showSharedMapData)
        {
            lastShowSharedMapData = ___m_showSharedMapData;
            explorationMaskDirty = true;
        }

        if (explorationMaskDirty)
        {
            RefreshExplorationMask(__instance, ___m_explored, ___m_exploredOthers, ___m_showSharedMapData);
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "UpdateBiome")]
    private static void UpdateBiomePostfix(
        Minimap __instance,
        bool[] ___m_explored,
        bool[] ___m_exploredOthers,
        bool ___m_showSharedMapData)
    {
        if (__instance.m_mode != Minimap.MapMode.Large
            || string.IsNullOrEmpty(__instance.m_biomeNameLarge.text)
            || !TryGetHoveredDanger(
                __instance,
                ___m_explored,
                ___m_exploredOthers,
                ___m_showSharedMapData,
                out WildernessDanger danger))
        {
            return;
        }

        __instance.m_biomeNameLarge.text += $" · {WildernessDangerScale.Label(danger)} wilderness";
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "Explore", typeof(int), typeof(int))]
    private static void ExplorePostfix(int x, int y, bool __result)
    {
        if (__result)
        {
            explorationMaskDirty = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "ExploreOthers")]
    private static void ExploreOthersPostfix(bool __result)
    {
        if (__result)
        {
            explorationMaskDirty = true;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "Reset")]
    private static void ResetPostfix()
    {
        explorationMaskDirty = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "ResetAndExplore", typeof(byte[]), typeof(byte[]))]
    private static void ResetAndExplorePostfix()
    {
        explorationMaskDirty = true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "OnDestroy")]
    private static void MinimapOnDestroyPostfix(Minimap __instance)
    {
        if (activeMap != __instance)
        {
            return;
        }

        if (overlayTexture != null)
        {
            Object.Destroy(overlayTexture);
        }

        activeMap = null;
        overlayImage = null;
        overlayTexture = null;
        explorationMaskDirty = false;
    }

    private static void Build(
        Minimap minimap,
        bool[] explored,
        bool[] exploredOthers,
        bool showSharedMapData)
    {
        activeMap = minimap;
        GameObject overlay = new(OverlayName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        RectTransform rect = (RectTransform)overlay.transform;
        rect.SetParent(minimap.m_mapImageLarge.transform, worldPositionStays: false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlayImage = overlay.GetComponent<RawImage>();
        overlayImage.raycastTarget = false;
        overlayImage.color = Color.white;

        overlayTexture = new Texture2D(
            minimap.m_textureSize,
            minimap.m_textureSize,
            TextureFormat.RGBA32,
            mipChain: false);
        overlayTexture.name = $"_{OverlayName}";
        overlayTexture.wrapMode = TextureWrapMode.Clamp;
        overlayTexture.filterMode = FilterMode.Point;
        overlayImage.texture = overlayTexture;

        GeneratePressureColors(minimap);
        lastShowSharedMapData = showSharedMapData;
        RefreshExplorationMask(minimap, explored, exploredOthers, showSharedMapData);
        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_map_built",
            $"texture_size={minimap.m_textureSize} explored_only=true labels=qualitative");
    }

    private static void GeneratePressureColors(Minimap minimap)
    {
        if (overlayTexture == null)
        {
            return;
        }

        int halfTexture = minimap.m_textureSize / 2;
        float halfPixel = minimap.m_pixelSize / 2f;
        Color32[] colors = new Color32[minimap.m_textureSize * minimap.m_textureSize];
        for (int y = 0; y < minimap.m_textureSize; y++)
        {
            for (int x = 0; x < minimap.m_textureSize; x++)
            {
                float worldX = (x - halfTexture) * minimap.m_pixelSize + halfPixel;
                float worldZ = (y - halfTexture) * minimap.m_pixelSize + halfPixel;
                Vector3 point = new(worldX, 0f, worldZ);
                Heightmap.Biome biome = WorldGenerator.instance.GetBiome(point);
                if (!BiomeStarChanceTuning.TryGetCurve(biome, out BiomeChanceCurve curve))
                {
                    colors[(y * minimap.m_textureSize) + x] = Color.clear;
                    continue;
                }

                float chance = WildernessStarChance.ComposeChance(
                    curve,
                    Utils.LengthXZ(point),
                    WorldGenerator.worldSize);
                colors[(y * minimap.m_textureSize) + x] = ColorFor(WildernessDangerScale.Classify(chance));
            }
        }

        overlayTexture.SetPixels32(colors);
        explorationMaskDirty = true;
    }

    private static void RefreshExplorationMask(
        Minimap minimap,
        bool[] explored,
        bool[] exploredOthers,
        bool showSharedMapData)
    {
        if (overlayTexture == null)
        {
            return;
        }

        Color32[] colors = overlayTexture.GetPixels32();
        int count = Mathf.Min(colors.Length, Mathf.Min(explored.Length, exploredOthers.Length));
        for (int index = 0; index < count; index++)
        {
            bool visible = WildernessDangerScale.IsVisible(
                explored[index],
                exploredOthers[index],
                showSharedMapData);
            colors[index].a = visible ? AlphaFor(colors[index]) : (byte)0;
        }

        overlayTexture.SetPixels32(colors);
        overlayTexture.Apply(updateMipmaps: false);
        explorationMaskDirty = false;
    }

    private static bool TryGetHoveredDanger(
        Minimap minimap,
        bool[] explored,
        bool[] exploredOthers,
        bool showSharedMapData,
        out WildernessDanger danger)
    {
        Vector2 screenPoint = ZInput.IsMouseActive()
            ? ZInput.mousePosition
            : new Vector2(Screen.width / 2f, Screen.height / 2f);
        RectTransform mapRect = minimap.m_mapImageLarge.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, screenPoint, null, out Vector2 localPoint))
        {
            danger = default;
            return false;
        }

        Vector2 normalized = Rect.PointToNormalized(mapRect.rect, localPoint);
        Rect visibleMap = minimap.m_mapImageLarge.uvRect;
        float mapX = visibleMap.xMin + (normalized.x * visibleMap.width);
        float mapY = visibleMap.yMin + (normalized.y * visibleMap.height);
        int halfTexture = minimap.m_textureSize / 2;
        Vector3 worldPoint = new(
            (mapX * minimap.m_textureSize - halfTexture) * minimap.m_pixelSize,
            0f,
            (mapY * minimap.m_textureSize - halfTexture) * minimap.m_pixelSize);
        int pixelX = Mathf.RoundToInt((worldPoint.x / minimap.m_pixelSize) + halfTexture);
        int pixelY = Mathf.RoundToInt((worldPoint.z / minimap.m_pixelSize) + halfTexture);
        if (pixelX < 0
            || pixelY < 0
            || pixelX >= minimap.m_textureSize
            || pixelY >= minimap.m_textureSize)
        {
            danger = default;
            return false;
        }

        int pixelIndex = (pixelY * minimap.m_textureSize) + pixelX;
        bool isVisible = WildernessDangerScale.IsVisible(
            explored[pixelIndex],
            exploredOthers[pixelIndex],
            showSharedMapData);
        if (!isVisible)
        {
            danger = default;
            return false;
        }

        float halfPixel = minimap.m_pixelSize / 2f;
        Vector3 sampledPoint = new(
            (pixelX - halfTexture) * minimap.m_pixelSize + halfPixel,
            0f,
            (pixelY - halfTexture) * minimap.m_pixelSize + halfPixel);
        Heightmap.Biome biome = WorldGenerator.instance.GetBiome(sampledPoint);
        if (!BiomeStarChanceTuning.TryGetCurve(biome, out BiomeChanceCurve curve))
        {
            danger = default;
            return false;
        }

        float chance = WildernessStarChance.ComposeChance(
            curve,
            Utils.LengthXZ(sampledPoint),
            WorldGenerator.worldSize);
        danger = WildernessDangerScale.Classify(chance);
        return true;
    }

    private static Color32 ColorFor(WildernessDanger danger)
    {
        return danger switch
        {
            WildernessDanger.Familiar => new Color32(91, 122, 88, 28),
            WildernessDanger.Sketchy => new Color32(176, 137, 57, 38),
            WildernessDanger.Dangerous => new Color32(190, 88, 45, 48),
            WildernessDanger.Deadly => new Color32(132, 31, 31, 62),
            _ => new Color32(0, 0, 0, 0),
        };
    }

    private static byte AlphaFor(Color32 color)
    {
        if (color.r == 91 && color.g == 122)
        {
            return 28;
        }

        if (color.r == 176 && color.g == 137)
        {
            return 38;
        }

        if (color.r == 190 && color.g == 88)
        {
            return 48;
        }

        if (color.r == 132 && color.g == 31)
        {
            return 62;
        }

        return 0;
    }
}
