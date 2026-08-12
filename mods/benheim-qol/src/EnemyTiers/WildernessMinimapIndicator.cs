using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

/// <summary>
/// Leaves Valheim's small-map biome label untouched and places one owned,
/// native-styled category line beneath the rendered biome text.
/// </summary>
internal static class WildernessMinimapIndicator
{
    private static string lastLogKey = "";
    private static string lastValidBiomeText = "";
    private static TMP_Text? categorySource;
    private static TMP_Text? categoryLabel;
    private static TMP_Text? measuredSource;
    private static string measuredText = "";
    private static TMP_FontAsset? measuredFont;
    private static Vector2 measuredRectSize;
    private static Vector4 measuredMargin;
    private static float measuredFontSize;
    private static float measuredFontSizeMin;
    private static float measuredFontSizeMax;
    private static float measuredCharacterSpacing;
    private static float measuredWordSpacing;
    private static FontStyles measuredFontStyle;
    private static TextAlignmentOptions measuredAlignment;
    private static bool measuredAutoSizing;
    private static Bounds measuredTextBounds;
    private static bool hasMeasuredTextBounds;

    internal static void Reset()
    {
        DestroyCategoryLabel();
        ClearNativeMeasurement();
        lastLogKey = "";
        lastValidBiomeText = "";
    }

    internal static void Update(Minimap minimap, WildernessDanger? currentDanger)
    {
        TMP_Text label = minimap.m_biomeNameSmall;
        if (label == null)
        {
            LogOnce("rejected:label_missing", "outcome=rejected reason=native_label_missing");
            return;
        }

        if (categorySource && categorySource != label)
        {
            DestroyCategoryLabel();
        }

        Player? player = Player.m_localPlayer;
        if (!player)
        {
            label.text = lastValidBiomeText;
            HideCategoryLabel();
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
            HideCategoryLabel();
            LogOnce(
                $"rejected:unresolved_biome:{biome}",
                $"outcome=rejected reason=unresolved_native_biome biome={biome} " +
                $"fallback={(string.IsNullOrEmpty(lastValidBiomeText) ? "empty" : "last_valid")}");
            return;
        }

        lastValidBiomeText = nativeBiome;
        label.text = nativeBiome;
        float centerOffset = 0f;
        if (currentDanger is WildernessDanger danger)
        {
            centerOffset = ShowCategoryLabel(label, nativeBiome, danger);
        }
        else
        {
            HideCategoryLabel();
        }

        string dangerValue = currentDanger?.ToString() ?? "none";
        LogOnce(
            $"rendered:{biome}:{dangerValue}",
            $"outcome=rendered biome={biome} danger={dangerValue} " +
            $"composition={(currentDanger.HasValue ? "separate_native_text" : "native_only")} " +
            $"category_center_offset={centerOffset:0.##}");
    }

    private static float ShowCategoryLabel(
        TMP_Text nativeLabel,
        string nativeBiome,
        WildernessDanger danger)
    {
        TMP_Text category = EnsureCategoryLabel(nativeLabel);
        if (!TryGetRenderedTextBounds(nativeLabel, nativeBiome, out Bounds nativeTextBounds))
        {
            HideCategoryLabel();
            LogOnce(
                $"rejected:native_mesh_not_ready:{nativeBiome}",
                "outcome=rejected reason=native_mesh_not_ready");
            return 0f;
        }

        SyncNativeStyle(nativeLabel, category);
        string categoryText = WildernessDangerScale.MapLabel(danger);
        category.text = categoryText;

        RectTransform nativeRect = nativeLabel.rectTransform;
        Vector2 categorySize = category.GetPreferredValues(
            categoryText,
            Mathf.Infinity,
            Mathf.Infinity);

        RectTransform categoryRect = category.rectTransform;
        categoryRect.anchorMin = nativeRect.pivot;
        categoryRect.anchorMax = nativeRect.pivot;
        categoryRect.anchoredPosition = new Vector2(
            nativeTextBounds.center.x,
            nativeTextBounds.min.y);
        categoryRect.sizeDelta = new Vector2(
            Mathf.Ceil(categorySize.x),
            Mathf.Ceil(categorySize.y));
        category.gameObject.SetActive(true);
        return nativeTextBounds.center.x;
    }

    private static TMP_Text EnsureCategoryLabel(TMP_Text nativeLabel)
    {
        if (categoryLabel && categorySource == nativeLabel)
        {
            return categoryLabel;
        }

        DestroyCategoryLabel();
        GameObject categoryObject = new("BenheimWildernessCategory", typeof(RectTransform));
        categoryObject.layer = nativeLabel.gameObject.layer;
        RectTransform categoryRect = (RectTransform)categoryObject.transform;
        categoryRect.SetParent(nativeLabel.rectTransform, worldPositionStays: false);
        categoryRect.pivot = new Vector2(0.5f, 1f);

        TextMeshProUGUI created = categoryObject.AddComponent<TextMeshProUGUI>();
        created.alignment = TextAlignmentOptions.Center;
        created.textWrappingMode = TextWrappingModes.NoWrap;
        created.overflowMode = TextOverflowModes.Overflow;
        created.richText = false;
        created.raycastTarget = false;
        created.margin = Vector4.zero;
        categorySource = nativeLabel;
        categoryLabel = created;
        return created;
    }

    private static bool TryGetRenderedTextBounds(
        TMP_Text source,
        string text,
        out Bounds textBounds)
    {
        RectTransform rect = source.rectTransform;
        bool signatureChanged = measuredSource != source
            || measuredText != text
            || measuredFont != source.font
            || measuredRectSize != rect.rect.size
            || measuredMargin != source.margin
            || !Mathf.Approximately(measuredFontSize, source.fontSize)
            || !Mathf.Approximately(measuredFontSizeMin, source.fontSizeMin)
            || !Mathf.Approximately(measuredFontSizeMax, source.fontSizeMax)
            || !Mathf.Approximately(measuredCharacterSpacing, source.characterSpacing)
            || !Mathf.Approximately(measuredWordSpacing, source.wordSpacing)
            || measuredFontStyle != source.fontStyle
            || measuredAlignment != source.alignment
            || measuredAutoSizing != source.enableAutoSizing;
        if (signatureChanged || !hasMeasuredTextBounds)
        {
            source.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: false);
            measuredSource = source;
            measuredText = text;
            measuredFont = source.font;
            measuredRectSize = rect.rect.size;
            measuredMargin = source.margin;
            measuredFontSize = source.fontSize;
            measuredFontSizeMin = source.fontSizeMin;
            measuredFontSizeMax = source.fontSizeMax;
            measuredCharacterSpacing = source.characterSpacing;
            measuredWordSpacing = source.wordSpacing;
            measuredFontStyle = source.fontStyle;
            measuredAlignment = source.alignment;
            measuredAutoSizing = source.enableAutoSizing;
            measuredTextBounds = source.textBounds;
            hasMeasuredTextBounds = measuredTextBounds.size.x > 0f
                && measuredTextBounds.size.y > 0f;
        }

        textBounds = measuredTextBounds;
        return hasMeasuredTextBounds;
    }

    private static void SyncNativeStyle(TMP_Text source, TMP_Text destination)
    {
        destination.font = source.font;
        destination.fontSharedMaterial = source.fontSharedMaterial;
        destination.color = source.color;
        destination.enableAutoSizing = false;
        destination.fontSize = source.fontSize;
        destination.fontStyle = source.fontStyle;
        destination.characterSpacing = source.characterSpacing;
        destination.wordSpacing = source.wordSpacing;
        destination.lineSpacing = source.lineSpacing;
        destination.paragraphSpacing = source.paragraphSpacing;
    }

    private static void HideCategoryLabel()
    {
        if (categoryLabel)
        {
            categoryLabel.gameObject.SetActive(false);
        }
    }

    private static void DestroyCategoryLabel()
    {
        if (categoryLabel)
        {
            Object.Destroy(categoryLabel.gameObject);
        }

        categorySource = null;
        categoryLabel = null;
    }

    private static void ClearNativeMeasurement()
    {
        measuredSource = null;
        measuredText = "";
        measuredFont = null;
        measuredTextBounds = default;
        hasMeasuredTextBounds = false;
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
