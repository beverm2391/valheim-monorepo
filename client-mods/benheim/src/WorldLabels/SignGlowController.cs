using TMPro;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal sealed class SignGlowController : MonoBehaviour
{
    private TextMeshProUGUI? widget;
    private Color originalColor;
    private Material? originalMaterial;
    private Material? glowMaterial;
    private bool restored;

    internal void Initialize(Sign sign)
    {
        widget = sign.m_textWidget;
        originalColor = widget.color;
        originalMaterial = widget.fontSharedMaterial;
        glowMaterial = WorldLabelStyle.CreateSignLetterMaterial(widget);

        widget.color = WorldLabelStyle.PortalAmber;
        widget.fontSharedMaterial = glowMaterial;
    }

    internal void RestoreAndRemove()
    {
        Restore();
        enabled = false;
        Destroy(this);
    }

    private void OnDestroy()
    {
        Restore();
        WorldLabelRuntime.Forget(this);
    }

    private void Restore()
    {
        if (restored)
        {
            return;
        }

        restored = true;
        if (widget != null)
        {
            widget.color = originalColor;
            widget.fontSharedMaterial = originalMaterial;
        }

        if (glowMaterial != null)
        {
            Destroy(glowMaterial);
            glowMaterial = null;
        }
    }
}
