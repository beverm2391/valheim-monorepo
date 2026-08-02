using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackTopLeftLayout
{
    private const float LeftOffset = 24f;
    private const float FallbackTopOffset = 120f;
    private const float HotbarGap = 8f;

    private static readonly FieldInfo MessageTextField =
        AccessTools.Field(typeof(MessageHud), "m_messageText");

    internal static void MoveBelowHotbar()
    {
        MessageHud messageHud = MessageHud.instance;
        if (!messageHud || MessageTextField.GetValue(messageHud) is not TMP_Text messageText)
        {
            return;
        }

        RectTransform rect = messageText.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(LeftOffset, -GetTopOffset(messageText));
        messageText.alignment = TextAlignmentOptions.TopLeft;
    }

    private static float GetTopOffset(TMP_Text messageText)
    {
        HotkeyBar hotkeyBar = Object.FindFirstObjectByType<HotkeyBar>();
        if (!hotkeyBar || hotkeyBar.transform is not RectTransform hotbarRect)
        {
            return FallbackTopOffset;
        }

        Canvas? canvas = messageText.canvas;
        Camera? uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;
        var corners = new Vector3[4];
        hotbarRect.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        float scaleFactor = canvas ? Mathf.Max(canvas.scaleFactor, 0.01f) : 1f;
        float hotbarBottomFromTop = (Screen.height - bottomLeft.y) / scaleFactor;
        return Mathf.Max(FallbackTopOffset, hotbarBottomFromTop + HotbarGap);
    }
}
