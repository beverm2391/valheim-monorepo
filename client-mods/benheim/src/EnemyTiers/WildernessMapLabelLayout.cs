using System;

namespace BenheimQoL.EnemyTiers;

internal readonly struct WildernessMapLabelBounds
{
    internal WildernessMapLabelBounds(float anchoredY, float sizeDeltaY)
    {
        AnchoredY = anchoredY;
        SizeDeltaY = sizeDeltaY;
    }

    internal float AnchoredY { get; }
    internal float SizeDeltaY { get; }
}

internal static class WildernessMapLabelLayout
{
    internal static bool IsResolvedNativeBiomeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        return trimmed.Length < 2 || trimmed[0] != '[' || trimmed[^1] != ']';
    }

    /// <summary>
    /// Adds room below a native RectTransform without moving its top edge.
    /// RectTransform size changes grow around the pivot, so preserving the top
    /// requires moving the anchored position by the portion that would
    /// otherwise grow up.
    /// </summary>
    internal static WildernessMapLabelBounds ExpandDownward(
        float nativeAnchoredY,
        float nativeSizeDeltaY,
        float pivotY,
        float addedHeight)
    {
        if (addedHeight < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(addedHeight));
        }

        return new WildernessMapLabelBounds(
            nativeAnchoredY - ((1f - pivotY) * addedHeight),
            nativeSizeDeltaY + addedHeight);
    }
}
