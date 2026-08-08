using System.Collections;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackReceiptHud
{
    private const float DurationSeconds = 5f;
    private const float FadeSeconds = 0.5f;
    private const float LeftFallback = 560f;
    private const float TopFallback = 48f;
    private const float HotbarGap = 8f;
    private const float ScreenMargin = 16f;

    private static readonly FieldInfo MessageTextField =
        AccessTools.Field(typeof(MessageHud), "m_messageText");
    private static readonly FieldInfo ElementsField =
        AccessTools.Field(typeof(HotkeyBar), "m_elements");
    private static readonly FieldInfo ElementGameObjectField =
        AccessTools.Field(AccessTools.Inner(typeof(HotkeyBar), "ElementData"), "m_go");

    private static TMP_Text? receiptText;
    private static float hideAt;

    internal static void Show(string message)
    {
        EnsureText();
        if (!receiptText)
        {
            return;
        }

        receiptText.text = message;
        receiptText.alpha = 1f;
        PlaceBesideHotbar(receiptText);
        receiptText.gameObject.SetActive(true);
        hideAt = Time.unscaledTime + DurationSeconds;
    }

    internal static void Update()
    {
        if (!receiptText || !receiptText.gameObject.activeSelf)
        {
            return;
        }

        float remaining = hideAt - Time.unscaledTime;
        if (remaining <= 0f)
        {
            receiptText.gameObject.SetActive(false);
            return;
        }

        receiptText.alpha = Mathf.Clamp01(remaining / FadeSeconds);
    }

    internal static void Destroy()
    {
        if (receiptText)
        {
            Object.Destroy(receiptText.gameObject);
            receiptText = null;
        }
    }

    private static void EnsureText()
    {
        if (receiptText)
        {
            return;
        }

        MessageHud messageHud = MessageHud.instance;
        TMP_Text? template = messageHud
            ? MessageTextField.GetValue(messageHud) as TMP_Text
            : null;
        if (!template)
        {
            return;
        }

        receiptText = Object.Instantiate(template, template.transform.parent);
        receiptText.name = "benheim-put-away-receipt";
        receiptText.alignment = TextAlignmentOptions.TopLeft;
        receiptText.gameObject.SetActive(false);
    }

    private static void PlaceBesideHotbar(TMP_Text text)
    {
        RectTransform rect = text.rectTransform;
        if (rect.parent is not RectTransform parent)
        {
            return;
        }

        Canvas? canvas = text.canvas;
        Camera? uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        float scaleFactor = canvas ? Mathf.Max(canvas.scaleFactor, 0.01f) : 1f;
        Rect? hotbarBounds = FindVisibleHotbarBounds(uiCamera);
        float margin = ScreenMargin * scaleFactor;
        float gap = HotbarGap * scaleFactor;
        float receiptWidth = Mathf.Min(
            Mathf.Max(rect.rect.width, text.preferredWidth) * scaleFactor,
            Mathf.Max(0f, Screen.width - margin * 2f));
        float receiptHeight = Mathf.Min(
            Mathf.Max(rect.rect.height, text.preferredHeight) * scaleFactor,
            Mathf.Max(0f, Screen.height - margin * 2f));
        Vector2 targetScreen = hotbarBounds.HasValue
            ? new Vector2(hotbarBounds.Value.xMax + gap, hotbarBounds.Value.yMax)
            : new Vector2(LeftFallback * scaleFactor, Screen.height - TopFallback * scaleFactor);

        // Keep the receipt in a separate right-side column. If it cannot fit
        // beside a wide hotbar, move it below the bar instead of sliding it
        // left over the slots or Valheim's top-left status lane.
        if (hotbarBounds.HasValue
            && targetScreen.x + receiptWidth + margin > Screen.width)
        {
            targetScreen.x = Screen.width - margin - receiptWidth;
            targetScreen.y = hotbarBounds.Value.yMin - gap;
        }

        targetScreen.x = Mathf.Clamp(
            targetScreen.x,
            margin,
            Mathf.Max(margin, Screen.width - margin - receiptWidth));
        targetScreen.y = Mathf.Clamp(
            targetScreen.y,
            margin + receiptHeight,
            Mathf.Max(margin + receiptHeight, Screen.height - margin));
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent,
            targetScreen,
            uiCamera,
            out Vector2 localPoint))
        {
            return;
        }

        rect.anchorMin = parent.pivot;
        rect.anchorMax = parent.pivot;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = localPoint;
    }

    private static Rect? FindVisibleHotbarBounds(Camera? uiCamera)
    {
        HotkeyBar hotkeyBar = Object.FindFirstObjectByType<HotkeyBar>();
        if (!hotkeyBar || ElementsField.GetValue(hotkeyBar) is not IEnumerable elements)
        {
            return null;
        }

        float right = float.NegativeInfinity;
        float top = float.NegativeInfinity;
        float left = float.PositiveInfinity;
        float bottom = float.PositiveInfinity;
        var corners = new Vector3[4];
        foreach (object element in elements)
        {
            GameObject? gameObject = ElementGameObjectField.GetValue(element) as GameObject;
            if (!gameObject || !gameObject.activeInHierarchy || gameObject.transform is not RectTransform rect)
            {
                continue;
            }

            rect.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            left = Mathf.Min(left, bottomLeft.x);
            bottom = Mathf.Min(bottom, bottomLeft.y);
            right = Mathf.Max(right, topRight.x);
            top = Mathf.Max(top, topRight.y);
        }

        // With an empty hotbar there may be no active element. The bar's own
        // live RectTransform still gives a UI-scale-aware anchor and avoids a
        // resolution-specific fallback.
        if (float.IsNegativeInfinity(right)
            && hotkeyBar.transform is RectTransform hotbarRect)
        {
            hotbarRect.GetWorldCorners(corners);
            Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            left = bottomLeft.x;
            bottom = bottomLeft.y;
            right = topRight.x;
            top = topRight.y;
        }

        return float.IsNegativeInfinity(right)
            ? null
            : Rect.MinMaxRect(left, bottom, right, top);
    }
}
