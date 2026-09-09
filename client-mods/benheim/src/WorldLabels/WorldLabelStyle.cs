using TMPro;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal static class WorldLabelStyle
{
    // A warm, restrained amber matches the wooden portal's light without
    // adding a light source or changing the sign board itself.
    internal static readonly Color PortalAmber = new(1f, 0.58f, 0.2f, 1f);

    private static readonly Color PortalAmberGlow = new(1.35f, 0.55f, 0.12f, 0.62f);

    internal static Material CreateSignLetterMaterial(TextMeshProUGUI widget)
    {
        Material material = new(widget.fontSharedMaterial)
        {
            name = "Benheim Sign Letter Glow",
            hideFlags = HideFlags.DontSave,
        };

        // TextMesh Pro's native SDF shader owns this soft letter halo. If a
        // future native donor drops one property, the amber face remains and
        // only the unsupported part of the glow is skipped.
        material.EnableKeyword("GLOW_ON");
        SetColorIfPresent(material, "_FaceColor", Color.white);
        SetColorIfPresent(material, "_GlowColor", PortalAmberGlow);
        SetFloatIfPresent(material, "_GlowOffset", 0f);
        SetFloatIfPresent(material, "_GlowInner", 0.02f);
        SetFloatIfPresent(material, "_GlowOuter", 0.12f);
        SetFloatIfPresent(material, "_GlowPower", 0.65f);
        return material;
    }

    private static void SetColorIfPresent(Material material, string property, Color value)
    {
        if (material.HasProperty(property))
        {
            material.SetColor(property, value);
        }
    }

    private static void SetFloatIfPresent(Material material, string property, float value)
    {
        if (material.HasProperty(property))
        {
            material.SetFloat(property, value);
        }
    }
}
