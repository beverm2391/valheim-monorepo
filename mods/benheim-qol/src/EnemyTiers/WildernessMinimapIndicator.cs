using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

/// <summary>
/// Leaves Valheim's small-map biome label untouched and places one owned,
/// native-styled category line beneath it on the same right edge.
/// </summary>
internal static class WildernessMinimapIndicator
{
    private static string lastLogKey = "";
    private static string lastValidBiomeText = "";
    private static TMP_Text? categorySource;
    private static TMP_Text? categoryLabel;

    internal static void Reset()
    {
        DestroyCategoryLabel();
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
        if (currentDanger is WildernessDanger danger)
        {
            ShowCategoryLabel(label, danger);
        }
        else
        {
            HideCategoryLabel();
        }

        string dangerValue = currentDanger?.ToString() ?? "none";
        LogOnce(
            $"rendered:{biome}:{dangerValue}",
            $"outcome=rendered biome={biome} danger={dangerValue} " +
            $"composition={(currentDanger.HasValue ? "separate_native_text" : "native_only")}");
    }

    private static void ShowCategoryLabel(TMP_Text nativeLabel, WildernessDanger danger)
    {
        TMP_Text category = EnsureCategoryLabel(nativeLabel);
        SyncNativeStyle(nativeLabel, category);
        category.text = WildernessDangerScale.MinimapLabel(danger);

        RectTransform nativeRect = nativeLabel.rectTransform;
        RectTransform categoryRect = category.rectTransform;
        categoryRect.anchorMin = new Vector2(0f, 0f);
        categoryRect.anchorMax = new Vector2(1f, 0f);
        categoryRect.pivot = new Vector2(0.5f, 1f);
        categoryRect.anchoredPosition = Vector2.zero;
        categoryRect.sizeDelta = new Vector2(0f, nativeRect.rect.height);
        category.gameObject.SetActive(true);
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

        TextMeshProUGUI created = categoryObject.AddComponent<TextMeshProUGUI>();
        created.textWrappingMode = TextWrappingModes.NoWrap;
        created.overflowMode = TextOverflowModes.Overflow;
        created.richText = false;
        created.raycastTarget = false;
        categorySource = nativeLabel;
        categoryLabel = created;
        return created;
    }

    private static void SyncNativeStyle(TMP_Text source, TMP_Text destination)
    {
        destination.font = source.font;
        destination.fontSharedMaterial = source.fontSharedMaterial;
        destination.color = source.color;
        destination.alignment = source.alignment;
        destination.margin = source.margin;
        destination.enableAutoSizing = source.enableAutoSizing;
        destination.fontSize = source.fontSize;
        destination.fontSizeMin = source.fontSizeMin;
        destination.fontSizeMax = source.fontSizeMax;
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
