using TMPro;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

/// <summary>
/// Strengthens Valheim's existing TMP outline only while a map label includes
/// a danger category. TMP's outline setters clone the font material, so this
/// owner keeps that clone only while the treatment is active. Deactivation
/// restores the exact native shared material and destroys the owned clone.
/// </summary>
internal sealed class WildernessMapLabelContrast
{
    private const float MinimumOutlineWidth = 0.14f;
    private static readonly Color32 ReadableOutline = new(18, 14, 10, 235);

    private TMP_Text? label;
    private Material? nativeSharedMaterial;
    private Material? ownedOutlineMaterial;
    private Color32 nativeOutlineColor;
    private float nativeOutlineWidth;
    private bool active;

    internal void SetActive(TMP_Text target, bool value)
    {
        if (label != target)
        {
            Restore();
            label = target;
            nativeSharedMaterial = target.fontSharedMaterial;
            nativeOutlineColor = target.outlineColor;
            nativeOutlineWidth = target.outlineWidth;
        }

        if (active == value)
        {
            return;
        }

        if (value)
        {
            target.outlineColor = ReadableOutline;
            target.outlineWidth = Mathf.Max(nativeOutlineWidth, MinimumOutlineWidth);
            ownedOutlineMaterial = target.fontSharedMaterial;
        }
        else
        {
            Restore();
            return;
        }

        active = value;
    }

    internal void Restore()
    {
        if (label && active && nativeSharedMaterial)
        {
            if (ownedOutlineMaterial && ownedOutlineMaterial != nativeSharedMaterial)
            {
                label.fontSharedMaterial = nativeSharedMaterial;
            }
            else
            {
                // TMP can reuse a pre-existing font material instead of
                // cloning. In that case our setters changed the native
                // instance and its exact outline values must be restored.
                label.outlineColor = nativeOutlineColor;
                label.outlineWidth = nativeOutlineWidth;
            }
        }

        if (ownedOutlineMaterial && ownedOutlineMaterial != nativeSharedMaterial)
        {
            Object.Destroy(ownedOutlineMaterial);
        }

        label = null;
        nativeSharedMaterial = null;
        ownedOutlineMaterial = null;
        active = false;
    }
}
