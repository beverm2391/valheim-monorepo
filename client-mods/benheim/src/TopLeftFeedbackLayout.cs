using System;

namespace BenheimQoL;

/// <summary>
/// Screen-space geometry for the Benheim-owned feedback lane. Keeping this
/// calculation free of Unity types lets the source proof exercise the exact
/// production placement rules across resolutions and UI scales.
/// </summary>
internal readonly struct TopLeftFeedbackRect
{
    internal TopLeftFeedbackRect(float xMin, float yMin, float width, float height)
    {
        XMin = xMin;
        YMin = yMin;
        XMax = xMin + width;
        YMax = yMin + height;
    }

    internal float XMin { get; }
    internal float YMin { get; }
    internal float XMax { get; }
    internal float YMax { get; }

    internal bool Overlaps(TopLeftFeedbackRect other)
    {
        return XMin < other.XMax
            && XMax > other.XMin
            && YMin < other.YMax
            && YMax > other.YMin;
    }
}

internal readonly struct TopLeftFeedbackPlacement
{
    internal TopLeftFeedbackPlacement(float x, float topY, float width, float height, bool usesHotbar)
    {
        X = x;
        TopY = topY;
        Width = width;
        Height = height;
        UsesHotbar = usesHotbar;
    }

    internal float X { get; }
    internal float TopY { get; }
    internal float Width { get; }
    internal float Height { get; }
    internal bool UsesHotbar { get; }

    internal TopLeftFeedbackRect Bounds =>
        new TopLeftFeedbackRect(X, TopY - Height, Width, Height);
}

internal static class TopLeftFeedbackLayout
{
    internal static TopLeftFeedbackPlacement Calculate(
        float screenWidth,
        float screenHeight,
        float scaleFactor,
        float laneWidth,
        float laneHeight,
        float gap,
        float margin,
        bool hasHotbar,
        TopLeftFeedbackRect hotbarBounds,
        bool hasNativeStatus,
        TopLeftFeedbackRect nativeStatusBounds,
        float fallbackLeft,
        float fallbackTopOffset)
    {
        float maximumX = MathF.Max(margin, screenWidth - margin - laneWidth);
        float targetX = hasHotbar
            ? Clamp(hotbarBounds.XMin, margin, maximumX)
            : Clamp(fallbackLeft * scaleFactor, margin, maximumX);
        float targetY = hasHotbar
            ? hotbarBounds.YMin - gap
            : screenHeight - fallbackTopOffset * scaleFactor;

        TopLeftFeedbackRect laneBounds =
            new TopLeftFeedbackRect(targetX, targetY - laneHeight, laneWidth, laneHeight);
        if (hasNativeStatus && laneBounds.Overlaps(nativeStatusBounds))
        {
            // The x anchor never changes. Collision resolution only moves the
            // lane lower, keeping it beneath the live hotbar.
            targetY = MathF.Min(targetY, nativeStatusBounds.YMin - gap);
        }

        // Clamp the complete lane to the visible screen after collision
        // resolution. Normal live-hotbar positions leave enough room for both
        // constraints; fallback placement remains safe on small resolutions.
        float minTop = laneHeight + margin;
        float maxTop = MathF.Max(minTop, screenHeight - margin);
        if (hasHotbar)
        {
            maxTop = MathF.Min(maxTop, hotbarBounds.YMin - gap);
        }

        targetY = Clamp(targetY, minTop, maxTop);
        return new TopLeftFeedbackPlacement(targetX, targetY, laneWidth, laneHeight, hasHotbar);
    }

    private static float Clamp(float value, float minimum, float maximum)
    {
        return MathF.Max(minimum, MathF.Min(maximum, value));
    }
}
