using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private const int OverlaySortingOrder = 1600;

    private static bool EnsureBuilt()
    {
        if (root != null)
        {
            return true;
        }

        NativeTemplates? templates = NativeTemplates.TryCreate();
        if (templates == null)
        {
            if (!buildFailureLogged)
            {
                buildFailureLogged = true;
                Plugin.Log.LogWarning("Could not build the shortcuts panel because native Valheim UI templates are not ready.");
            }
            return false;
        }

        root = CreateRectObject(RootName, templates.Canvas.transform).gameObject;
        root.SetActive(false);
        Canvas overlayCanvas = root.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = OverlaySortingOrder;
        root.AddComponent<GraphicRaycaster>();

        RectTransform blockerRect = (RectTransform)root.transform;
        Stretch(blockerRect);
        Image blocker = root.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0.56f);
        blocker.raycastTarget = true;

        windowRect = CreateRectObject("Window", blockerRect);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        Image window = windowRect.gameObject.AddComponent<Image>();
        CopyImageStyle(templates.PanelBackground, window);
        window.raycastTarget = true;

        BuildHeader(windowRect, templates);
        BuildTabBar(windowRect, templates);

        contentScroll = CreateNativeScrollView(windowRect, templates);
        RectTransform scrollRect = (RectTransform)contentScroll.transform;
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(28f, 72f);
        scrollRect.offsetMax = new Vector2(-28f, -150f);
        contentRect = contentScroll.content;
        BuildPages(contentRect!, templates);

        BuildFooter(windowRect, templates);
        buildFailureLogged = false;
        Diagnostics.Event("Shortcuts", "panel_built", "template=inventory_gui layout=tabs");
        return true;
    }

    private static void BuildHeader(RectTransform parent, NativeTemplates templates)
    {
        TMP_Text title = CreateText("Title", parent, templates.TitleText, layoutElement: false);
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0.72f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(34f, -74f);
        titleRect.offsetMax = new Vector2(0f, -18f);
        title.text = "Benheim";
        title.fontSize = 40f;
        title.alignment = TextAlignmentOptions.Left;

        TMP_Text version = CreateText("Version", parent, templates.Text, layoutElement: false);
        RectTransform versionRect = (RectTransform)version.transform;
        versionRect.anchorMin = new Vector2(0.72f, 1f);
        versionRect.anchorMax = new Vector2(1f, 1f);
        versionRect.pivot = new Vector2(1f, 1f);
        versionRect.offsetMin = new Vector2(0f, -64f);
        versionRect.offsetMax = new Vector2(-36f, -24f);
        version.text = $"v{Plugin.PluginVersion}";
        version.fontSize = 20f;
        version.color = new Color(0.8f, 0.82f, 0.84f, 1f);
        version.alignment = TextAlignmentOptions.Right;
    }

    private static void BuildFooter(RectTransform parent, NativeTemplates templates)
    {
        TMP_Text footer = CreateText("FooterHelp", parent, templates.Text, layoutElement: false);
        RectTransform footerRect = (RectTransform)footer.transform;
        footerRect.anchorMin = Vector2.zero;
        footerRect.anchorMax = new Vector2(0.72f, 0f);
        footerRect.pivot = Vector2.zero;
        footerRect.offsetMin = new Vector2(34f, 18f);
        footerRect.offsetMax = new Vector2(0f, 58f);
        footer.text = "Left Shift + B / Escape    Close     F7    Export diagnostic log";
        footer.fontSize = 17f;
        footer.color = new Color(0.72f, 0.74f, 0.76f, 1f);
        footer.alignment = TextAlignmentOptions.Left;

        closeButton = CreateNativeButton("CloseButton", parent, templates, "Close");
        RectTransform closeRect = (RectTransform)closeButton.transform;
        closeRect.anchorMin = new Vector2(1f, 0f);
        closeRect.anchorMax = new Vector2(1f, 0f);
        closeRect.pivot = new Vector2(1f, 0f);
        closeRect.anchoredPosition = new Vector2(-28f, 18f);
        closeRect.sizeDelta = new Vector2(190f, 46f);
        closeButton.onClick.AddListener(Hide);
    }
}
