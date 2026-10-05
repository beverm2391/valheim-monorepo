using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BenheimQoL.SpawnProtection;

/// <summary>
/// One owned HUD row for the local player's native spawn protection. The rings
/// approximate horizontal coverage; this reading uses Valheim's actual collider
/// check, so elevated pieces and terrain height still affect the indicator.
/// </summary>
internal static class SpawnProtectionMinimapIndicator
{
    private const string IndicatorText = "[no_spawn]";
    private static readonly Color IndicatorColor = new(0.65f, 0.65f, 0.65f, 1f);
    private static TMP_Text? source;
    private static TMP_Text? label;
    private static bool lastVisible;
    private static string lastReason = "";

    internal static void Update()
    {
        Minimap? minimap = Minimap.instance;
        Player? player = Player.m_localPlayer;
        TMP_Text? native = minimap != null ? minimap.m_biomeNameSmall : null;
        if (!SpawnProtectionOverlay.Enabled || minimap == null || player == null
            || minimap.m_mode != Minimap.MapMode.Small || native == null)
        {
            Hide("view_unavailable");
            return;
        }

        // Query at the same 10 Hz cadence as the rings, using the allocation-free
        // native check. No cached circle approximation or separate enable state.
        if (EffectArea.IsPointInsideArea(player.transform.position, EffectArea.Type.PlayerBase) == null)
        {
            Hide("outside_coverage");
            return;
        }

        RectTransform? danger = native.transform.Find("BenheimWildernessCategory") as RectTransform;
        if (source != native || label == null)
        {
            DestroyLabel();
            source = native;
            TMP_Text? donor = danger != null ? danger.GetComponent<TMP_Text>() : null;
            label = Object.Instantiate(donor != null ? donor : native, native.rectTransform);
            label.gameObject.name = "BenheimSpawnProtectionMinimap";

            // Clone a loaded text donor with its font already assigned before
            // TMP initializes. Bare TMP construction emits a missing-default-font
            // warning in Valheim. If danger presentation is unavailable, the
            // native biome donor also has animation, outline, and child rows;
            // remove those only from our clone so this remains one inert label.
            foreach (Transform child in label.transform)
            {
                child.gameObject.SetActive(false);
                Object.Destroy(child.gameObject);
            }
            Animator? animator = label.GetComponent<Animator>();
            if (animator != null) { animator.enabled = false; Object.Destroy(animator); }
            UnityEngine.UI.Outline? outline = label.GetComponent<UnityEngine.UI.Outline>();
            if (outline != null) { outline.enabled = false; Object.Destroy(outline); }

            label.font = native.font;
            label.fontSharedMaterial = native.fontSharedMaterial;
            label.alignment = native.alignment;
            label.margin = native.margin;
            label.fontSize = native.fontSize;
            label.fontStyle = native.fontStyle;
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.richText = false;
            label.raycastTarget = false;
            label.color = IndicatorColor;
            label.text = IndicatorText;
        }

        // Stay on the native right edge below the danger row, reserving its live
        // height only when visible. Parenting follows native HUD/UI scaling and
        // visibility; no fixed screen coordinate or sibling layout is changed.
        float dangerHeight = danger != null && danger.gameObject.activeSelf ? danger.rect.height : 0f;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -dangerHeight);
        rect.sizeDelta = new Vector2(0f, native.rectTransform.rect.height);
        label.gameObject.SetActive(true);
        Emit(true, "native_playerbase");
    }

    internal static void Reset()
    {
        if (label != null) Hide("overlay_off");
        DestroyLabel();
        lastVisible = false;
        lastReason = "";
    }

    private static void Hide(string reason)
    {
        if (label != null) label.gameObject.SetActive(false);
        Emit(false, reason);
    }

    private static void DestroyLabel()
    {
        if (label != null)
        {
            label.gameObject.SetActive(false);
            Object.Destroy(label.gameObject);
        }
        label = null;
        source = null;
    }

    private static void Emit(bool visible, string reason)
    {
        if (lastVisible == visible && lastReason == reason) return;
        lastVisible = visible;
        lastReason = reason;
        Diagnostics.Emit(DiagnosticEvent.Create("SpawnProtection", "minimap_indicator")
            .Boolean("visible", visible).Boolean("enabled", SpawnProtectionOverlay.Enabled)
            .String("reason", reason).String("text", IndicatorText).String("color", "grey"));
    }
}
