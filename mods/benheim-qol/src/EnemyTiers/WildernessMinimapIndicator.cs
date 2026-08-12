using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

/// <summary>
/// Composes the danger category into Valheim's native small-map biome label.
/// Native bounds remain the source of truth so the second line grows downward
/// without moving the biome name or accumulating rect changes.
/// </summary>
internal static class WildernessMinimapIndicator
{
    private static readonly WildernessMapLabelContrast LabelContrast = new();

    private static string lastLogKey = "";
    private static string lastValidBiomeText = "";
    private static TMP_Text? expandedLabel;
    private static Vector2 nativeAnchoredPosition;
    private static Vector2 nativeSizeDelta;
    private static bool boundsExpanded;
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
            bool ownsComposedText = boundsExpanded && measuredLabel == expandedLabel;
            string renderedNativeText = measuredNativeText;
            RestoreNativeBounds(expandedLabel);
            if (ownsComposedText
                && WildernessMapLabelLayout.IsResolvedNativeBiomeText(renderedNativeText))
            {
                expandedLabel.text = renderedNativeText;
            }
        }

        LabelContrast.Restore();
        lastLogKey = "";
        lastValidBiomeText = "";
        expandedLabel = null;
        boundsExpanded = false;
        measuredLabel = null;
        measuredNativeText = "";
    }

    internal static void Update(Minimap minimap, WildernessDanger? currentDanger)
    {
        TMP_Text label = minimap.m_biomeNameSmall;
        if (label == null)
        {
            LogOnce("rejected:label_missing", "outcome=rejected reason=native_label_missing");
            return;
        }

        RestoreNativeBounds(label);
        Player? player = Player.m_localPlayer;
        if (!player)
        {
            label.text = lastValidBiomeText;
            LabelContrast.SetActive(label, value: false);
            LogOnce("rejected:player_missing", "outcome=rejected reason=player_missing");
            return;
        }

        Heightmap.Biome biome = player.GetCurrentBiome();
        string nativeBiome = Localization.instance.Localize(
            "$biome_" + biome.ToString().ToLowerInvariant());
        if (biome == Heightmap.Biome.None
            || !WildernessMapLabelLayout.IsResolvedNativeBiomeText(nativeBiome))
        {
            label.text = lastValidBiomeText;
            LabelContrast.SetActive(label, value: false);
            LogOnce(
                $"rejected:unresolved_biome:{biome}",
                $"outcome=rejected reason=unresolved_native_biome biome={biome} " +
                $"fallback={(string.IsNullOrEmpty(lastValidBiomeText) ? "empty" : "last_valid")}");
            return;
        }

        lastValidBiomeText = nativeBiome;
        if (currentDanger is WildernessDanger danger)
        {
            string combinedText = $"{nativeBiome}\n{WildernessDangerScale.StyledMapLabel(danger)}";
            float addedHeight = GetAddedHeight(label, nativeBiome, combinedText, danger);
            label.text = combinedText;
            ExpandBoundsDownward(label, addedHeight);
            LabelContrast.SetActive(label, value: true);
        }
        else
        {
            label.text = nativeBiome;
            LabelContrast.SetActive(label, value: false);
        }

        string dangerValue = currentDanger?.ToString() ?? "none";
        LogOnce(
            $"rendered:{biome}:{dangerValue}",
            $"outcome=rendered biome={biome} danger={dangerValue}");
    }

    private static void RestoreNativeBounds(TMP_Text label)
    {
        RectTransform rect = label.rectTransform;
        if (expandedLabel != label)
        {
            if (expandedLabel && boundsExpanded)
            {
                expandedLabel.rectTransform.anchoredPosition = nativeAnchoredPosition;
                expandedLabel.rectTransform.sizeDelta = nativeSizeDelta;
                if (measuredLabel == expandedLabel
                    && WildernessMapLabelLayout.IsResolvedNativeBiomeText(measuredNativeText))
                {
                    expandedLabel.text = measuredNativeText;
                }
            }

            expandedLabel = label;
            nativeAnchoredPosition = rect.anchoredPosition;
            nativeSizeDelta = rect.sizeDelta;
            boundsExpanded = false;
            return;
        }

        if (!boundsExpanded)
        {
            nativeAnchoredPosition = rect.anchoredPosition;
            nativeSizeDelta = rect.sizeDelta;
            return;
        }

        rect.anchoredPosition = nativeAnchoredPosition;
        rect.sizeDelta = nativeSizeDelta;
        boundsExpanded = false;
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

    private static void ExpandBoundsDownward(TMP_Text label, float addedHeight)
    {
        RectTransform rect = label.rectTransform;
        WildernessMapLabelBounds bounds = WildernessMapLabelLayout.ExpandDownward(
            nativeAnchoredPosition.y,
            nativeSizeDelta.y,
            rect.pivot.y,
            addedHeight);
        rect.anchoredPosition = new Vector2(nativeAnchoredPosition.x, bounds.AnchoredY);
        rect.sizeDelta = new Vector2(nativeSizeDelta.x, bounds.SizeDeltaY);
        boundsExpanded = true;
    }

    private static void LogOnce(string key, string fields)
    {
        if (lastLogKey == key)
        {
            return;
        }

        lastLogKey = key;
        Diagnostics.Event("EnemyTiers", "wilderness_minimap_indicator", fields);
    }
}
