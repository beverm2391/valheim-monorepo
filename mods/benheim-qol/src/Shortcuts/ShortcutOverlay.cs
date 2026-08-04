using System;
using System.Collections.Generic;
using System.Linq;
using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private const string RootName = "BenheimShortcutsPanel";
    private const float StatusRefreshInterval = 0.25f;
    private static GameObject? root;
    private static RectTransform? windowRect;
    private static RectTransform? contentRect;
    private static TMP_Text? multiplayerStatus;
    private static Button? closeButton;
    private static bool visible;
    private static bool buildFailureLogged;
    private static bool previousCursorVisible;
    private static CursorLockMode previousCursorLock;
    private static float nextStatusRefreshAt;
    private static float nextBuildAttemptAt;
    private static string lastStatusFingerprint = string.Empty;
    private static int lastScreenWidth;
    private static int lastScreenHeight;

    internal static bool IsOpen => visible;

    internal static void Update()
    {
        if (root == null && Time.unscaledTime >= nextBuildAttemptAt)
        {
            nextBuildAttemptAt = Time.unscaledTime + 1f;
            EnsureBuilt();
        }

        if (!visible)
        {
            if (MenuShortcutDown()
                && !InputState.IsTextEntryActive()
                && !Menu.IsVisible())
            {
                Show();
            }
            return;
        }

        if (root == null)
        {
            visible = false;
            RestoreCursor();
            return;
        }

        if (MenuShortcutDown() || RawKeyDown(KeyCode.Escape))
        {
            Hide();
            return;
        }

        ResizeWindowIfNeeded();
        RefreshMultiplayerStatus();
    }

    internal static void Destroy()
    {
        if (visible)
        {
            RestoreCursor();
        }

        visible = false;
        if (root != null)
        {
            UnityEngine.Object.Destroy(root);
        }

        root = null;
        windowRect = null;
        contentRect = null;
        multiplayerStatus = null;
        closeButton = null;
        lastStatusFingerprint = string.Empty;
    }

    private static void Show()
    {
        if (!EnsureBuilt())
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        visible = true;
        root!.SetActive(true);
        root.transform.SetAsLastSibling();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        lastStatusFingerprint = string.Empty;
        nextStatusRefreshAt = float.NegativeInfinity;
        ResizeWindowIfNeeded(force: true);
        RefreshMultiplayerStatus(force: true);
        EventSystem.current?.SetSelectedGameObject(closeButton!.gameObject);
        Diagnostics.Event("Shortcuts", "panel_toggled", "visible=true ui=native");
    }

    private static void Hide()
    {
        visible = false;
        if (root != null)
        {
            root.SetActive(false);
        }

        GameObject? selected = EventSystem.current?.currentSelectedGameObject;
        if (selected != null && root != null && selected.transform.IsChildOf(root.transform))
        {
            EventSystem.current!.SetSelectedGameObject(null);
        }

        RestoreCursor();
        Diagnostics.Event("Shortcuts", "panel_toggled", "visible=false ui=native");
    }

    private static void RestoreCursor()
    {
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLock;
    }

    private static ScrollRect CreateNativeScrollView(RectTransform parent, NativeTemplates templates)
    {
        RectTransform scrollRoot = CreateRectObject("ScrollView", parent);
        Image background = scrollRoot.gameObject.AddComponent<Image>();
        CopyImageStyle(templates.Scroll?.GetComponent<Image>() ?? templates.PanelBackground, background);
        ScrollRect scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        if (templates.Scroll != null)
        {
            scroll.movementType = templates.Scroll.movementType;
            scroll.elasticity = templates.Scroll.elasticity;
            scroll.inertia = templates.Scroll.inertia;
            scroll.decelerationRate = templates.Scroll.decelerationRate;
            scroll.scrollSensitivity = templates.Scroll.scrollSensitivity;
        }

        RectTransform viewport = CreateRectObject("Viewport", scrollRoot);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(12f, 12f);
        viewport.offsetMax = new Vector2(-34f, -12f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        Image? nativeViewportImage = templates.Scroll?.viewport?.GetComponent<Image>();
        CopyImageStyle(nativeViewportImage ?? templates.PanelBackground, viewportImage);
        Mask mask = viewport.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform content = CreateRectObject("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 12, 12);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Scrollbar scrollbar = CreateNativeScrollbar(
            "Scrollbar",
            scrollRoot,
            templates.Scrollbar,
            templates.PanelBackground);
        RectTransform scrollbarRect = (RectTransform)scrollbar.transform;
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-24f, 12f);
        scrollbarRect.offsetMax = new Vector2(-8f, -12f);

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scroll.verticalScrollbarSpacing = -3f;
        return scroll;
    }

    private static Scrollbar CreateNativeScrollbar(
        string name,
        RectTransform parent,
        Scrollbar? template,
        Image fallback)
    {
        RectTransform rootRect = CreateRectObject(name, parent);
        Image background = rootRect.gameObject.AddComponent<Image>();
        Image? templateBackground = template?.GetComponent<Image>();
        CopyImageStyle(templateBackground ?? fallback, background);

        RectTransform slidingArea = CreateRectObject("Sliding Area", rootRect);
        Stretch(slidingArea, 3f);
        RectTransform handle = CreateRectObject("Handle", slidingArea);
        Stretch(handle);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        Image? templateHandle = template?.handleRect?.GetComponent<Image>();
        CopyImageStyle(templateHandle ?? templateBackground ?? fallback, handleImage);

        Scrollbar scrollbar = rootRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handle;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        if (template != null)
        {
            scrollbar.transition = template.transition;
            scrollbar.colors = template.colors;
            scrollbar.spriteState = template.spriteState;
            scrollbar.animationTriggers = template.animationTriggers;
            scrollbar.navigation = template.navigation;
        }
        return scrollbar;
    }

    private static Button CreateNativeButton(string name, RectTransform parent, NativeTemplates templates)
    {
        RectTransform rect = CreateRectObject(name, parent);
        Image image = rect.gameObject.AddComponent<Image>();
        Image? templateImage = templates.Button?.targetGraphic as Image
            ?? templates.Button?.GetComponent<Image>();
        CopyImageStyle(templateImage ?? templates.PanelBackground, image);

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (templates.Button != null)
        {
            button.transition = templates.Button.transition;
            button.colors = templates.Button.colors;
            button.spriteState = templates.Button.spriteState;
            button.animationTriggers = templates.Button.animationTriggers;
            button.navigation = templates.Button.navigation;
        }

        TMP_Text label = CreateText("Label", rect, templates.Text, layoutElement: false);
        RectTransform labelRect = (RectTransform)label.transform;
        Stretch(labelRect, 4f);
        label.text = "Close";
        label.fontSize = 21f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static TMP_Text CreateText(
        string name,
        RectTransform parent,
        TMP_Text template,
        bool layoutElement)
    {
        RectTransform rect = CreateRectObject(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = template.font;
        text.fontSharedMaterial = template.fontSharedMaterial;
        text.color = template.color;
        text.fontSize = template.fontSize;
        text.fontStyle = template.fontStyle;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.richText = true;
        text.raycastTarget = false;
        if (layoutElement)
        {
            rect.gameObject.AddComponent<LayoutElement>();
        }
        return text;
    }

    private static NativeTemplates? FindNativeTemplates()
    {
        Image? panel = FindPanelBackground();
        Button? button = FindNativeComponent<Button>(candidate => candidate.targetGraphic is Image);
        ScrollRect? scroll = FindNativeComponent<ScrollRect>(candidate => candidate.viewport != null && candidate.content != null);
        Scrollbar? scrollbar = scroll?.verticalScrollbar
            ?? FindNativeComponent<Scrollbar>(candidate => candidate.handleRect != null);
        TMP_Text? text = button?.GetComponentInChildren<TMP_Text>(includeInactive: true)
            ?? FindNativeComponent<TMP_Text>(candidate => candidate.font != null);
        return panel != null && text != null
            ? new NativeTemplates(panel, button, scroll, scrollbar, text)
            : null;
    }

    private static Canvas? FindNativeCanvas()
    {
        return InventoryGui.instance?.GetComponentInParent<Canvas>()
            ?? Hud.instance?.GetComponentInParent<Canvas>()
            ?? FindNativeComponent<Canvas>(_ => true);
    }

    private static Image? FindPanelBackground()
    {
        IEnumerable<Image> candidates = NativeCandidates<Image>();
        return candidates
            .Where(candidate => candidate.sprite != null)
            .Where(candidate => candidate.GetComponent<Button>() == null)
            .Where(candidate => candidate.GetComponentInParent<Scrollbar>() == null)
            .OrderByDescending(candidate => candidate.type == Image.Type.Sliced)
            .ThenByDescending(candidate =>
            {
                Rect rect = ((RectTransform)candidate.transform).rect;
                return Math.Abs(rect.width * rect.height);
            })
            .FirstOrDefault();
    }

    private static T? FindNativeComponent<T>(Func<T, bool> predicate) where T : Component
    {
        return NativeCandidates<T>().FirstOrDefault(predicate);
    }

    private static IEnumerable<T> NativeCandidates<T>() where T : Component
    {
        if (InventoryGui.instance != null)
        {
            foreach (T component in InventoryGui.instance.GetComponentsInChildren<T>(includeInactive: true))
            {
                yield return component;
            }
        }
        if (Hud.instance != null)
        {
            foreach (T component in Hud.instance.GetComponentsInChildren<T>(includeInactive: true))
            {
                yield return component;
            }
        }

        foreach (T component in Resources.FindObjectsOfTypeAll<T>())
        {
            if (component != null
                && component.gameObject.scene.IsValid()
                && (root == null || !component.transform.IsChildOf(root.transform)))
            {
                yield return component;
            }
        }
    }

    private static void CopyImageStyle(Image source, Image destination)
    {
        destination.sprite = source.sprite;
        destination.overrideSprite = source.overrideSprite;
        destination.type = source.type;
        destination.preserveAspect = source.preserveAspect;
        destination.fillCenter = source.fillCenter;
        destination.fillMethod = source.fillMethod;
        destination.fillAmount = source.fillAmount;
        destination.fillClockwise = source.fillClockwise;
        destination.fillOrigin = source.fillOrigin;
        destination.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
        destination.material = source.material;
        destination.color = source.color;
    }

    private static RectTransform CreateRectObject(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        gameObject.layer = parent.gameObject.layer;
        RectTransform rect = (RectTransform)gameObject.transform;
        rect.SetParent(parent, worldPositionStays: false);
        return rect;
    }

    private static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static void ResizeWindowIfNeeded(bool force = false)
    {
        if (windowRect == null
            || (!force && Screen.width == lastScreenWidth && Screen.height == lastScreenHeight))
        {
            return;
        }

        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        windowRect.sizeDelta = new Vector2(
            Mathf.Clamp(Screen.width - 96f, 520f, 1100f),
            Mathf.Clamp(Screen.height - 120f, 420f, 820f));
    }

    private static bool RawKeyDown(KeyCode key)
    {
        return Input.GetKeyDown(key) || ZInput.GetKeyDown(key);
    }

    private static bool MenuShortcutDown()
    {
        return RawKeyDown(KeyCode.B)
            && (Input.GetKey(KeyCode.LeftShift)
                || ZInput.GetKey(KeyCode.LeftShift));
    }

    private sealed class NativeTemplates
    {
        internal NativeTemplates(
            Image panelBackground,
            Button? button,
            ScrollRect? scroll,
            Scrollbar? scrollbar,
            TMP_Text text)
        {
            PanelBackground = panelBackground;
            Button = button;
            Scroll = scroll;
            Scrollbar = scrollbar;
            Text = text;
        }

        internal Image PanelBackground { get; }
        internal Button? Button { get; }
        internal ScrollRect? Scroll { get; }
        internal Scrollbar? Scrollbar { get; }
        internal TMP_Text Text { get; }
    }
}
