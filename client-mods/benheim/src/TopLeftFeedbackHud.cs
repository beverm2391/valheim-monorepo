using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BenheimQoL;

internal enum TopLeftFeedbackResult
{
    Unavailable,
    CreatedNotPlaced,
    Placed
}

/// <summary>
/// Owns the small Benheim feedback lane below the live hotbar.
///
/// Native MessageHud top-left messages stay on their native object. Benheim
/// callers use this lane for their own feedback so native status text can be
/// observed and avoided without moving, patching, or restyling it.
/// </summary>
internal static class TopLeftFeedbackHud
{
    private const float GroupedDurationSeconds = 5f;
    private const float TransientDurationSeconds = 4f;
    private const float FadeSeconds = 0.5f;
    private const float FallbackLeft = 24f;
    private const float FallbackTopOffset = 120f;
    private const float HotbarGap = 8f;
    private const float ScreenMargin = 16f;
    private const float VisibleAlphaThreshold = 0.01f;

    private static readonly FieldInfo MessageTextField =
        AccessTools.Field(typeof(MessageHud), "m_messageText");
    private static readonly FieldInfo ElementsField =
        AccessTools.Field(typeof(HotkeyBar), "m_elements");
    private static readonly FieldInfo ElementGameObjectField =
        AccessTools.Field(AccessTools.Inner(typeof(HotkeyBar), "ElementData"), "m_go");
    private static readonly Vector3[] WorldCorners = new Vector3[4];
    private static readonly List<Vector2> EntrySizes = new List<Vector2>();

    private sealed class Entry
    {
        internal Entry(TMP_Text text, float durationSeconds, float fadeSeconds, bool transient)
        {
            Text = text;
            HideAt = Time.unscaledTime + durationSeconds;
            FadeSeconds = fadeSeconds;
            Transient = transient;
        }

        internal TMP_Text Text { get; }
        internal float HideAt { get; set; }
        internal float FadeSeconds { get; }
        internal bool Transient { get; }
    }

    private static readonly List<Entry> Entries = new List<Entry>();

    /// <summary>
    /// Shows grouped feedback such as Put Away and Mass Repair. Grouped lines
    /// remain intact for their full receipt duration when short feedback arrives.
    /// </summary>
    internal static void ShowGrouped(string message)
    {
        Add(message, GroupedDurationSeconds, transient: false);
    }

    /// <summary>
    /// Shows a short Benheim-owned top-left confirmation. Repeating the same
    /// or another short message refreshes the one transient slot instead of
    /// growing an unhelpful duplicate stack. Grouped entries remain intact.
    /// </summary>
    internal static TopLeftFeedbackResult ShowTransient(string message)
    {
        PruneDestroyedEntries();
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry entry = Entries[i];
            if (!entry.Transient)
            {
                continue;
            }

            Object.Destroy(entry.Text.gameObject);
            Entries.RemoveAt(i);
        }

        // A new transient replaces the previous transient but never hides a
        // grouped receipt, preserving all grouped content while keeping short
        // feedback timely and readable.
        TMP_Text? text = CreateText();
        if (!text)
        {
            return TopLeftFeedbackResult.Unavailable;
        }

        text.text = message;
        text.alpha = 1f;
        text.gameObject.SetActive(true);
        Entries.Add(new Entry(text, TransientDurationSeconds, FadeSeconds, transient: true));
        return PlaceEntriesBelowHotbar()
            && text.gameObject.activeInHierarchy
            && text.canvasRenderer.GetAlpha() > VisibleAlphaThreshold
            && text.alpha > VisibleAlphaThreshold
            ? TopLeftFeedbackResult.Placed
            : TopLeftFeedbackResult.CreatedNotPlaced;
    }

    internal static void Update()
    {
        PruneDestroyedEntries();
        if (Entries.Count == 0)
        {
            return;
        }

        float now = Time.unscaledTime;
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            Entry entry = Entries[i];
            float remaining = entry.HideAt - now;
            if (remaining <= 0f)
            {
                if (entry.Text)
                {
                    Object.Destroy(entry.Text.gameObject);
                }

                Entries.RemoveAt(i);
                continue;
            }

            entry.Text.alpha = Mathf.Clamp01(remaining / entry.FadeSeconds);
        }

        if (Entries.Count > 0)
        {
            // Re-evaluate every frame: native exposed/sheltered status text may
            // appear after Benheim feedback was already shown, and resolution or
            // UI-scale changes can move the live hotbar while the lane is visible.
            PlaceEntriesBelowHotbar();
        }
    }

    internal static void Destroy()
    {
        foreach (Entry entry in Entries)
        {
            if (entry.Text)
            {
                Object.Destroy(entry.Text.gameObject);
            }
        }

        Entries.Clear();
    }

    private static void PruneDestroyedEntries()
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
        {
            if (!Entries[i].Text || !Entries[i].Text.gameObject)
            {
                Entries.RemoveAt(i);
            }
        }
    }

    private static void Add(string message, float durationSeconds, bool transient)
    {
        PruneDestroyedEntries();
        TMP_Text? text = CreateText();
        if (!text)
        {
            return;
        }

        text.text = message;
        text.alpha = 1f;
        text.gameObject.SetActive(true);
        Entries.Add(new Entry(text, durationSeconds, FadeSeconds, transient));
        PlaceEntriesBelowHotbar();
    }

    private static TMP_Text? CreateText()
    {
        MessageHud messageHud = MessageHud.instance;
        TMP_Text? template = messageHud
            ? MessageTextField.GetValue(messageHud) as TMP_Text
            : null;
        if (!template || template.transform.parent is not Transform parent)
        {
            return null;
        }

        TMP_Text text = Object.Instantiate(template, parent);
        text.name = "benheim-top-left-feedback";
        text.alignment = TextAlignmentOptions.TopLeft;
        // Valheim keeps m_messageText hidden with CrossFadeAlpha(0). The clone
        // inherits that CanvasRenderer alpha; TMP_Text.alpha changes only the
        // text color and cannot make the hidden renderer visible.
        text.canvasRenderer.SetAlpha(1f);
        text.gameObject.SetActive(false);
        return text;
    }

    private static bool PlaceEntriesBelowHotbar()
    {
        PruneDestroyedEntries();
        if (Entries.Count == 0 || Entries[0].Text.rectTransform.parent is not RectTransform parent)
        {
            return false;
        }

        TMP_Text firstText = Entries[0].Text;
        Canvas? canvas = firstText.canvas;
        Camera? uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        float scaleFactor = canvas ? Mathf.Max(canvas.scaleFactor, 0.01f) : 1f;
        float margin = ScreenMargin * scaleFactor;
        float gap = HotbarGap * scaleFactor;

        EntrySizes.Clear();
        float laneWidth = 0f;
        float laneHeight = 0f;
        for (int i = 0; i < Entries.Count; i++)
        {
            TMP_Text text = Entries[i].Text;
            RectTransform rect = text.rectTransform;
            float width = Mathf.Max(rect.rect.width, text.preferredWidth) * scaleFactor;
            float height = Mathf.Max(rect.rect.height, text.preferredHeight) * scaleFactor;
            EntrySizes.Add(new Vector2(width, height));
            laneWidth = Mathf.Max(laneWidth, width);
            laneHeight += height;
            if (i > 0)
            {
                laneHeight += gap;
            }
        }

        Rect? hotbarBounds = FindVisibleHotbarBounds(uiCamera);
        Rect? nativeStatusBounds = FindVisibleNativeStatusBounds(uiCamera);
        TopLeftFeedbackPlacement placement = TopLeftFeedbackLayout.Calculate(
            Screen.width,
            Screen.height,
            scaleFactor,
            laneWidth,
            laneHeight,
            gap,
            margin,
            hotbarBounds.HasValue,
            hotbarBounds.HasValue ? ToLayoutRect(hotbarBounds.Value) : default,
            nativeStatusBounds.HasValue,
            nativeStatusBounds.HasValue ? ToLayoutRect(nativeStatusBounds.Value) : default,
            FallbackLeft,
            FallbackTopOffset);

        float currentTop = placement.TopY;
        for (int i = 0; i < Entries.Count; i++)
        {
            TMP_Text text = Entries[i].Text;
            RectTransform rect = text.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent,
                new Vector2(placement.X, currentTop),
                uiCamera,
                out Vector2 localPoint))
            {
                return false;
            }

            rect.anchorMin = parent.pivot;
            rect.anchorMax = parent.pivot;
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = localPoint;
            currentTop -= EntrySizes[i].y + gap;
        }

        return true;
    }

    private static TopLeftFeedbackRect ToLayoutRect(Rect rect)
    {
        return new TopLeftFeedbackRect(rect.xMin, rect.yMin, rect.width, rect.height);
    }

    private static Rect? FindVisibleHotbarBounds(Camera? uiCamera)
    {
        HotkeyBar hotkeyBar = Object.FindFirstObjectByType<HotkeyBar>();
        if (!hotkeyBar)
        {
            return null;
        }

        float right = float.NegativeInfinity;
        float top = float.NegativeInfinity;
        float left = float.PositiveInfinity;
        float bottom = float.PositiveInfinity;
        if (ElementsField.GetValue(hotkeyBar) is IEnumerable elements)
        {
            foreach (object element in elements)
            {
                GameObject? gameObject = ElementGameObjectField.GetValue(element) as GameObject;
                if (!gameObject || !gameObject.activeInHierarchy || gameObject.transform is not RectTransform rect)
                {
                    continue;
                }

                rect.GetWorldCorners(WorldCorners);
                Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, WorldCorners[0]);
                Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, WorldCorners[2]);
                left = Mathf.Min(left, bottomLeft.x);
                bottom = Mathf.Min(bottom, bottomLeft.y);
                right = Mathf.Max(right, topRight.x);
                top = Mathf.Max(top, topRight.y);
            }
        }

        // An empty hotbar has no element objects. The live bar transform still
        // provides a UI-scale-aware anchor for the lane.
        if (float.IsNegativeInfinity(right)
            && hotkeyBar.transform is RectTransform hotbarRect)
        {
            hotbarRect.GetWorldCorners(WorldCorners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, WorldCorners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, WorldCorners[2]);
            left = bottomLeft.x;
            bottom = bottomLeft.y;
            right = topRight.x;
            top = topRight.y;
        }

        return float.IsNegativeInfinity(right)
            ? null
            : Rect.MinMaxRect(left, bottom, right, top);
    }

    private static Rect? FindVisibleNativeStatusBounds(Camera? uiCamera)
    {
        Rect? bounds = null;
        MessageHud messageHud = MessageHud.instance;
        if (messageHud
            && MessageTextField.GetValue(messageHud) is TMP_Text messageText
            && messageText.gameObject.activeInHierarchy
            && !string.IsNullOrWhiteSpace(messageText.text)
            && messageText.canvasRenderer.GetAlpha() > VisibleAlphaThreshold)
        {
            bounds = GetScreenBounds(messageText.rectTransform, uiCamera);
        }

        // Valheim also renders persistent status names such as Sheltered and
        // Exposed as active children of this root. Measure only those live
        // entries, not the root itself, so an unrelated empty layout rectangle
        // cannot push the Benheim lane down.
        RectTransform? statusRoot = Hud.instance ? Hud.instance.m_statusEffectListRoot : null;
        if (statusRoot)
        {
            for (int i = 0; i < statusRoot.childCount; i++)
            {
                if (statusRoot.GetChild(i) is not RectTransform status
                    || !status.gameObject.activeInHierarchy)
                {
                    continue;
                }

                bounds = Union(bounds, GetScreenBounds(status, uiCamera));
            }
        }

        return bounds;
    }

    private static Rect GetScreenBounds(RectTransform rect, Camera? uiCamera)
    {
        rect.GetWorldCorners(WorldCorners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, WorldCorners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, WorldCorners[2]);
        return Rect.MinMaxRect(bottomLeft.x, bottomLeft.y, topRight.x, topRight.y);
    }

    private static Rect Union(Rect? current, Rect addition)
    {
        if (!current.HasValue)
        {
            return addition;
        }

        Rect existing = current.Value;
        return Rect.MinMaxRect(
            Mathf.Min(existing.xMin, addition.xMin),
            Mathf.Min(existing.yMin, addition.yMin),
            Mathf.Max(existing.xMax, addition.xMax),
            Mathf.Max(existing.yMax, addition.yMax));
    }
}
