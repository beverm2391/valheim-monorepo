using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

internal static class WildernessMapHover
{
    private static readonly HashSet<string> LoggedHoverWindows = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "UpdateBiome")]
    private static void UpdateBiomePostfix(
        Minimap __instance,
        bool[] ___m_explored,
        bool[] ___m_exploredOthers,
        bool ___m_showSharedMapData)
    {
        if (__instance.m_mode != Minimap.MapMode.Large
            || WorldGenerator.instance == null
            || string.IsNullOrEmpty(__instance.m_biomeNameLarge.text)
            || !TryGetHoveredDanger(
                __instance,
                ___m_explored,
                ___m_exploredOthers,
                ___m_showSharedMapData,
                out HoveredDanger hovered))
        {
            return;
        }

        __instance.m_biomeNameLarge.text += $" · {WildernessDangerScale.Label(hovered.Danger)} wilderness";
        LogHover(hovered);
    }

    private static bool TryGetHoveredDanger(
        Minimap minimap,
        bool[] explored,
        bool[] exploredOthers,
        bool showSharedMapData,
        out HoveredDanger hovered)
    {
        Vector2 screenPoint = ZInput.IsMouseActive()
            ? ZInput.mousePosition
            : new Vector2(Screen.width / 2f, Screen.height / 2f);
        RectTransform mapRect = minimap.m_mapImageLarge.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, screenPoint, null, out Vector2 localPoint))
        {
            hovered = default;
            return false;
        }

        Vector2 normalized = Rect.PointToNormalized(mapRect.rect, localPoint);
        Rect visibleMap = minimap.m_mapImageLarge.uvRect;
        float mapX = visibleMap.xMin + (normalized.x * visibleMap.width);
        float mapY = visibleMap.yMin + (normalized.y * visibleMap.height);
        int halfTexture = minimap.m_textureSize / 2;
        int pixelX = Mathf.RoundToInt(mapX * minimap.m_textureSize);
        int pixelY = Mathf.RoundToInt(mapY * minimap.m_textureSize);
        if (pixelX < 0
            || pixelY < 0
            || pixelX >= minimap.m_textureSize
            || pixelY >= minimap.m_textureSize)
        {
            hovered = default;
            return false;
        }

        int pixelIndex = (pixelY * minimap.m_textureSize) + pixelX;
        if (pixelIndex >= explored.Length
            || pixelIndex >= exploredOthers.Length
            || !WildernessDangerScale.IsVisible(
                explored[pixelIndex],
                exploredOthers[pixelIndex],
                showSharedMapData))
        {
            hovered = default;
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
            hovered = default;
            return false;
        }

        float distance = Utils.LengthXZ(sampledPoint);
        float normalizedDistance = WildernessStarChance.NormalizeDistance(
            distance,
            WorldGenerator.worldSize);
        float chance = WildernessStarChance.ComposeChance(
            curve,
            distance,
            WorldGenerator.worldSize);
        hovered = new HoveredDanger(
            biome,
            distance,
            normalizedDistance,
            chance,
            WildernessDangerScale.Classify(chance));
        return true;
    }

    private static void LogHover(HoveredDanger hovered)
    {
        int distanceWindow = (int)MathF.Floor(hovered.NormalizedDistance * 10f);
        string key = $"{hovered.Biome}:{hovered.Danger}:{distanceWindow}";
        if (!LoggedHoverWindows.Add(key))
        {
            return;
        }

        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_map_hover",
            $"source=ordinary_wilderness explored_only=true " +
            $"biome={hovered.Biome} " +
            $"distance={hovered.Distance:0} " +
            $"distance_ratio={hovered.NormalizedDistance:0.###} " +
            $"adjusted_chance={hovered.Chance:0.###} " +
            $"danger={hovered.Danger}");
    }

    private readonly struct HoveredDanger
    {
        internal HoveredDanger(
            Heightmap.Biome biome,
            float distance,
            float normalizedDistance,
            float chance,
            WildernessDanger danger)
        {
            Biome = biome;
            Distance = distance;
            NormalizedDistance = normalizedDistance;
            Chance = chance;
            Danger = danger;
        }

        internal Heightmap.Biome Biome { get; }
        internal float Distance { get; }
        internal float NormalizedDistance { get; }
        internal float Chance { get; }
        internal WildernessDanger Danger { get; }
    }
}
