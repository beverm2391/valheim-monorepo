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
    private const float LeftFallback = 24f;
    private const float TopFallback = 120f;
    private const float HotbarGap = 8f;

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
        PlaceBelowHotbar(receiptText);
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

    private static void PlaceBelowHotbar(TMP_Text text)
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
        Vector2 targetScreen = FindVisibleHotbarBottomLeft(uiCamera)
            ?? new Vector2(LeftFallback * scaleFactor, Screen.height - TopFallback * scaleFactor);
        targetScreen.y -= HotbarGap * scaleFactor;
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

    private static Vector2? FindVisibleHotbarBottomLeft(Camera? uiCamera)
    {
        HotkeyBar hotkeyBar = Object.FindFirstObjectByType<HotkeyBar>();
        if (!hotkeyBar || ElementsField.GetValue(hotkeyBar) is not IEnumerable elements)
        {
            return null;
        }

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
            left = Mathf.Min(left, bottomLeft.x);
            bottom = Mathf.Min(bottom, bottomLeft.y);
        }

        return float.IsPositiveInfinity(left)
            ? null
            : new Vector2(left, bottom);
    }
}
