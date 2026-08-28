using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Valheim.SettingsGui;

namespace BenheimQoL.Shortcuts;

// Valheim keeps InventoryGui instantiated while playing, even when its root is hidden.
// These exact donors are preferable to scanning every loaded Unity object and hoping the
// largest or first candidate happens to be the intended style.
internal sealed class NativeTemplates
{
    private static readonly FieldInfo? CameraShakeToggleField =
        AccessTools.Field(typeof(AccessibilitySettings), "m_cameraShake");

    private const string PanelPath = "root/Crafting/Bkg";
    private const string ButtonPath = "root/Container/TakeAll";
    private const string ScrollPath = "root/Container/ContainerGrid";
    private const string ScrollbarPath = "root/Container/ContainerScroll";
    private const string TextPath = "root/Crafting/Decription/Description";
    private const string TitleTextPath = "root/Container/container_name";
    private const string ButtonTextPath = "root/Container/TakeAll/Text";

    private NativeTemplates(
        Canvas canvas,
        Image panelBackground,
        Button? button,
        ScrollRect? scroll,
        Scrollbar? scrollbar,
        Toggle checkbox,
        TMP_Text text,
        TMP_Text titleText,
        TMP_Text buttonText)
    {
        Canvas = canvas;
        PanelBackground = panelBackground;
        Button = button;
        Scroll = scroll;
        Scrollbar = scrollbar;
        Checkbox = checkbox;
        Text = text;
        TitleText = titleText;
        ButtonText = buttonText;
    }

    internal Canvas Canvas { get; }
    internal Image PanelBackground { get; }
    internal Button? Button { get; }
    internal ScrollRect? Scroll { get; }
    internal Scrollbar? Scrollbar { get; }
    internal Toggle Checkbox { get; }
    internal TMP_Text Text { get; }
    internal TMP_Text TitleText { get; }
    internal TMP_Text ButtonText { get; }

    internal static NativeTemplates? TryCreate()
    {
        InventoryGui? inventory = InventoryGui.instance;
        if (inventory == null)
        {
            return null;
        }

        Canvas? canvas = inventory.GetComponent<Canvas>()
            ?? inventory.GetComponentInParent<Canvas>();
        Image? panel = FindComponent<Image>(inventory.transform, PanelPath);
        TMP_Text? text = FindComponent<TMP_Text>(inventory.transform, TextPath);
        Toggle? checkbox = TryFindNativeCheckbox();
        if (canvas == null || panel == null || text == null || checkbox == null)
        {
            return null;
        }

        return new NativeTemplates(
            canvas,
            panel,
            FindComponent<Button>(inventory.transform, ButtonPath),
            FindComponent<ScrollRect>(inventory.transform, ScrollPath),
            FindComponent<Scrollbar>(inventory.transform, ScrollbarPath),
            checkbox,
            text,
            FindComponent<TMP_Text>(inventory.transform, TitleTextPath) ?? text,
            FindComponent<TMP_Text>(inventory.transform, ButtonTextPath) ?? text);
    }

    private static Toggle? TryFindNativeCheckbox()
    {
        GameObject? settingsPrefab = Menu.instance?.m_settingsPrefab;
        AccessibilitySettings? accessibility =
            settingsPrefab?.GetComponentInChildren<AccessibilitySettings>(includeInactive: true);
        return accessibility == null
            ? null
            : CameraShakeToggleField?.GetValue(accessibility) as Toggle;
    }

    private static T? FindComponent<T>(Transform owner, string path) where T : Component
    {
        return owner.Find(path)?.GetComponent<T>();
    }
}
