using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

[HarmonyPatch]
internal static class WildernessMapHover
{
    private static readonly WildernessMapLabelContrast LabelContrast = new();
    private static readonly HashSet<HoverProbeStage> LoggedProbeStages = new();
    private static readonly HashSet<int> LoggedExplorationStates = new();
    private static readonly HashSet<Heightmap.Biome> LoggedUnsupportedBiomes = new();
    private static readonly HashSet<string> LoggedClassifications = new();

    private static TMP_Text? expandedLabel;
    private static Vector2 nativeAnchoredPosition;
    private static Vector2 nativeSizeDelta;
    private static bool labelBoundsExpanded;

    private static TMP_Text? measuredLabel;
    private static string measuredNativeText = "";
    private static WildernessDanger measuredDanger;
    private static float measuredFontSize;
    private static float measuredWidth;
    private static float measuredAddedHeight;

    internal static void Reset()
    {
        if (expandedLabel)
        {
            bool ownsComposedText = labelBoundsExpanded && measuredLabel == expandedLabel;
            string renderedNativeText = measuredNativeText;
            RestoreNativeLabelBounds(expandedLabel);
            if (ownsComposedText
                && WildernessMapLabelLayout.IsResolvedNativeBiomeText(renderedNativeText))
            {
                expandedLabel.text = renderedNativeText;
            }
        }

        LabelContrast.Restore();
        expandedLabel = null;
        labelBoundsExpanded = false;
        measuredLabel = null;
        measuredNativeText = "";
        measuredAddedHeight = 0f;
        LoggedProbeStages.Clear();
        LoggedExplorationStates.Clear();
        LoggedUnsupportedBiomes.Clear();
        LoggedClassifications.Clear();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Minimap), "UpdateBiome")]
    private static void UpdateBiomePostfix(
        Minimap __instance,
        bool[] ___m_explored,
        bool[] ___m_exploredOthers,
        bool ___m_showSharedMapData)
    {
        TMP_Text label = __instance.m_biomeNameLarge;
        RestoreNativeLabelBounds(label);

        LogProbeStageOnce(HoverProbeStage.PatchInvoked, $"map_mode={__instance.m_mode}");
        if (__instance.m_mode != Minimap.MapMode.Large)
        {
            LabelContrast.SetActive(label, value: false);
            LogProbeStageOnce(HoverProbeStage.NotLargeMap, $"map_mode={__instance.m_mode}");
            return;
        }

        LogProbeStageOnce(HoverProbeStage.LargeMapReady, "map_mode=Large");
        if (WorldGenerator.instance == null)
        {
            LabelContrast.SetActive(label, value: false);
            LogProbeStageOnce(HoverProbeStage.WorldGeneratorMissing);
            return;
        }

        if (string.IsNullOrEmpty(label.text))
        {
            LabelContrast.SetActive(label, value: false);
            LogProbeStageOnce(HoverProbeStage.NativeBiomeLabelEmpty);
            return;
        }

        if (!WildernessMapLabelLayout.IsResolvedNativeBiomeText(label.text))
        {
            label.text = "";
            LabelContrast.SetActive(label, value: false);
            LogProbeStageOnce(HoverProbeStage.NativeBiomeLabelUnresolved);
            return;
        }

        if (!TryGetHoveredDanger(
            __instance,
            ___m_explored,
            ___m_exploredOthers,
            ___m_showSharedMapData,
            out HoveredDanger hovered))
        {
            LabelContrast.SetActive(label, value: false);
            return;
        }

        string nativeText = label.text;
        string combinedText = $"{nativeText}\n{WildernessDangerScale.StyledMapLabel(hovered.Danger)}";
        float addedHeight = GetAddedHeight(label, nativeText, combinedText, hovered.Danger);
        label.text = combinedText;
        ExpandLabelBoundsDownward(label, addedHeight);
        LabelContrast.SetActive(label, value: true);
        LogHover(hovered);
    }

    private static void RestoreNativeLabelBounds(TMP_Text label)
    {
        RectTransform rect = label.rectTransform;
        if (expandedLabel != label)
        {
            if (expandedLabel && labelBoundsExpanded)
            {
                expandedLabel.rectTransform.anchoredPosition = nativeAnchoredPosition;
                expandedLabel.rectTransform.sizeDelta = nativeSizeDelta;
            }

            expandedLabel = label;
            nativeAnchoredPosition = rect.anchoredPosition;
            nativeSizeDelta = rect.sizeDelta;
            labelBoundsExpanded = false;
            return;
        }

        if (!labelBoundsExpanded)
        {
            nativeAnchoredPosition = rect.anchoredPosition;
            nativeSizeDelta = rect.sizeDelta;
            return;
        }

        rect.anchoredPosition = nativeAnchoredPosition;
        rect.sizeDelta = nativeSizeDelta;
        labelBoundsExpanded = false;
    }

    private static float GetAddedHeight(
        TMP_Text label,
        string nativeText,
        string combinedText,
        WildernessDanger danger)
    {
        float width = label.rectTransform.rect.width;
        if (measuredLabel == label
            && measuredNativeText == nativeText
            && measuredDanger == danger
            && Mathf.Approximately(measuredFontSize, label.fontSize)
            && Mathf.Approximately(measuredWidth, width))
        {
            return measuredAddedHeight;
        }

        float nativeHeight = label.GetPreferredValues(nativeText, width, Mathf.Infinity).y;
        float combinedHeight = label.GetPreferredValues(combinedText, width, Mathf.Infinity).y;
        measuredLabel = label;
        measuredNativeText = nativeText;
        measuredDanger = danger;
        measuredFontSize = label.fontSize;
        measuredWidth = width;
        measuredAddedHeight = Mathf.Max(0f, combinedHeight - nativeHeight);
        return measuredAddedHeight;
    }

    private static void ExpandLabelBoundsDownward(TMP_Text label, float addedHeight)
    {
        RectTransform rect = label.rectTransform;
        WildernessMapLabelBounds bounds = WildernessMapLabelLayout.ExpandDownward(
            nativeAnchoredPosition.y,
            nativeSizeDelta.y,
            rect.pivot.y,
            addedHeight);
        rect.anchoredPosition = new Vector2(nativeAnchoredPosition.x, bounds.AnchoredY);
        rect.sizeDelta = new Vector2(nativeSizeDelta.x, bounds.SizeDeltaY);
        labelBoundsExpanded = true;
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
            LogProbeStageOnce(
                HoverProbeStage.LocalPointRejected,
                $"input={(ZInput.IsMouseActive() ? "mouse" : "controller")}");
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
            LogProbeStageOnce(
                HoverProbeStage.BoundsRejected,
                $"pixel_x={pixelX} pixel_y={pixelY} texture_size={minimap.m_textureSize}");
            hovered = default;
            return false;
        }

        int pixelIndex = (pixelY * minimap.m_textureSize) + pixelX;
        if (pixelIndex >= explored.Length || pixelIndex >= exploredOthers.Length)
        {
            LogProbeStageOnce(
                HoverProbeStage.ExplorationArrayRejected,
                $"pixel_index={pixelIndex} local_count={explored.Length} shared_count={exploredOthers.Length}");
            hovered = default;
            return false;
        }

        bool locallyExplored = explored[pixelIndex];
        bool sharedExplored = exploredOthers[pixelIndex];
        if (!WildernessDangerScale.IsVisible(
            locallyExplored,
            sharedExplored,
            showSharedMapData))
        {
            LogExplorationVisibilityOnce(
                locallyExplored,
                sharedExplored,
                showSharedMapData,
                visible: false);
            hovered = default;
            return false;
        }

        LogExplorationVisibilityOnce(
            locallyExplored,
            sharedExplored,
            showSharedMapData,
            visible: true);

        float halfPixel = minimap.m_pixelSize / 2f;
        Vector3 sampledPoint = new(
            (pixelX - halfTexture) * minimap.m_pixelSize + halfPixel,
            0f,
            (pixelY - halfTexture) * minimap.m_pixelSize + halfPixel);
        Heightmap.Biome biome = WorldGenerator.instance.GetBiome(sampledPoint);
        if (!BiomeStarChanceTuning.TryGetCurve(biome, out BiomeChanceCurve curve))
        {
            LogUnsupportedBiomeOnce(biome);
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
            locallyExplored,
            sharedExplored,
            showSharedMapData,
            WildernessDangerScale.Classify(chance));
        return true;
    }

    private static void LogHover(HoveredDanger hovered)
    {
        string key = $"{hovered.Biome}:{hovered.Danger}";
        if (!LoggedClassifications.Add(key))
        {
            return;
        }

        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_map_hover",
            $"stage=classified source=ordinary_wilderness explored_only=true " +
            $"local_explored={hovered.LocallyExplored.ToString().ToLowerInvariant()} " +
            $"shared_explored={hovered.SharedExplored.ToString().ToLowerInvariant()} " +
            $"show_shared={hovered.ShowSharedMapData.ToString().ToLowerInvariant()} " +
            $"biome={hovered.Biome} " +
            $"distance={hovered.Distance:0} " +
            $"distance_ratio={hovered.NormalizedDistance:0.###} " +
            $"adjusted_chance={hovered.Chance:0.###} " +
            $"danger={hovered.Danger}");
    }

    private static void LogProbeStageOnce(HoverProbeStage stage, string fields = "")
    {
        if (!LoggedProbeStages.Add(stage))
        {
            return;
        }

        string suffix = string.IsNullOrEmpty(fields) ? "" : $" {fields}";
        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_map_hover_probe",
            $"stage={StageName(stage)}{suffix}");
    }

    private static void LogExplorationVisibilityOnce(
        bool locallyExplored,
        bool sharedExplored,
        bool showSharedMapData,
        bool visible)
    {
        int state = (locallyExplored ? 1 : 0)
            | (sharedExplored ? 2 : 0)
            | (showSharedMapData ? 4 : 0);
        if (!LoggedExplorationStates.Add(state))
        {
            return;
        }

        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_map_hover_probe",
            $"stage={(visible ? "exploration_visible" : "exploration_hidden")} " +
            $"local_explored={locallyExplored.ToString().ToLowerInvariant()} " +
            $"shared_explored={sharedExplored.ToString().ToLowerInvariant()} " +
            $"show_shared={showSharedMapData.ToString().ToLowerInvariant()}");
    }

    private static void LogUnsupportedBiomeOnce(Heightmap.Biome biome)
    {
        if (!LoggedUnsupportedBiomes.Add(biome))
        {
            return;
        }

        Diagnostics.Event(
            "EnemyTiers",
            "wilderness_map_hover_probe",
            $"stage=unsupported_biome biome={biome}");
    }

    private static string StageName(HoverProbeStage stage)
    {
        return stage switch
        {
            HoverProbeStage.PatchInvoked => "patch_invoked",
            HoverProbeStage.NotLargeMap => "not_large_map",
            HoverProbeStage.LargeMapReady => "large_map_ready",
            HoverProbeStage.WorldGeneratorMissing => "world_generator_missing",
            HoverProbeStage.NativeBiomeLabelEmpty => "native_biome_label_empty",
            HoverProbeStage.NativeBiomeLabelUnresolved => "native_biome_label_unresolved",
            HoverProbeStage.LocalPointRejected => "local_point_rejected",
            HoverProbeStage.BoundsRejected => "bounds_rejected",
            HoverProbeStage.ExplorationArrayRejected => "exploration_array_rejected",
            _ => "unknown",
        };
    }

}
