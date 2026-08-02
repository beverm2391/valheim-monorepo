using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.Infrastructure;

internal static class WorldFeedback
{
    private const float DurationSeconds = 3f;
    private const float FadeSeconds = 0.5f;
    private const float MaxDistance = 40f;
    private const float LabelWidth = 440f;

    private static readonly List<WorldMessage> Messages = new List<WorldMessage>();
    private static GUIStyle? labelStyle;

    internal static void ShowAbovePlayer(Player player, string text)
    {
        ShowAbove(player.transform, Vector3.up * 1.9f, text);
    }

    internal static void ShowAbove(Transform anchor, Vector3 offset, string text)
    {
        for (int index = Messages.Count - 1; index >= 0; index--)
        {
            if (Messages[index].Anchor == anchor)
            {
                Messages.RemoveAt(index);
            }
        }

        Messages.Add(new WorldMessage(anchor, offset, text, Time.unscaledTime + DurationSeconds));
    }

    internal static void Update()
    {
        float now = Time.unscaledTime;
        for (int index = Messages.Count - 1; index >= 0; index--)
        {
            WorldMessage message = Messages[index];
            if (message.ExpiresAt <= now || !message.Anchor)
            {
                Messages.RemoveAt(index);
            }
        }
    }

    internal static void Draw()
    {
        if (Messages.Count == 0 || Hud.IsUserHidden())
        {
            return;
        }

        Camera camera = Utils.GetMainCamera();
        if (!camera)
        {
            return;
        }

        EnsureStyle();
        Color originalColor = GUI.color;
        foreach (WorldMessage message in Messages)
        {
            Vector3 worldPosition = message.GetWorldPosition();
            Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z <= 0f
                || Vector3.Distance(camera.transform.position, worldPosition) > MaxDistance)
            {
                continue;
            }

            var content = new GUIContent(message.Text);
            float height = labelStyle!.CalcHeight(content, LabelWidth);
            var rect = new Rect(
                screenPosition.x - LabelWidth / 2f,
                Screen.height - screenPosition.y - height / 2f,
                LabelWidth,
                height);
            float alpha = Mathf.Clamp01((message.ExpiresAt - Time.unscaledTime) / FadeSeconds);

            GUI.color = new Color(0f, 0f, 0f, 0.8f * alpha);
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), content, labelStyle);
            GUI.color = new Color(1f, 0.82f, 0.28f, alpha);
            GUI.Label(rect, content, labelStyle);
        }

        GUI.color = originalColor;
    }

    internal static void Clear()
    {
        Messages.Clear();
    }

    private static void EnsureStyle()
    {
        if (labelStyle != null)
        {
            return;
        }

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
        };
    }

    private sealed class WorldMessage
    {
        internal WorldMessage(Transform anchor, Vector3 offset, string text, float expiresAt)
        {
            Anchor = anchor;
            Offset = offset;
            Text = text;
            ExpiresAt = expiresAt;
        }

        internal Transform Anchor { get; }
        internal Vector3 Offset { get; }
        internal string Text { get; }
        internal float ExpiresAt { get; }

        internal Vector3 GetWorldPosition()
        {
            return Anchor.position + Offset;
        }
    }
}
