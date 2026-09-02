using TMPro;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal sealed class PortalLabelController : MonoBehaviour
{
    private TeleportWorld portal = null!;
    private GameObject? labelRoot;
    private TextMeshProUGUI? frontLabel;
    private TextMeshProUGUI? backLabel;
    private Material? frontGlowMaterial;
    private Material? backGlowMaterial;
    private string? currentTag;
    private bool disposed;

    internal void Initialize(TeleportWorld source)
    {
        portal = source;
        InvokeRepeating(
            nameof(Refresh),
            0f,
            PortalSignVisualFactory.RefreshIntervalSeconds);
    }

    internal void DisposeAndRemove()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelInvoke(nameof(Refresh));
        DisposeVisual();
        enabled = false;
        Destroy(this);
    }

    private void Refresh()
    {
        if (disposed || portal == null)
        {
            return;
        }

        string tag = portal.GetText();
        if (string.IsNullOrEmpty(tag))
        {
            currentTag = tag;
            DisposeVisual();
            return;
        }

        if (labelRoot == null && !TryBuildLabel(tag))
        {
            WorldLabelRuntime.LogNativeSignPending();
            return;
        }

        if (tag == currentTag)
        {
            return;
        }

        currentTag = tag;
        frontLabel!.text = tag;
        backLabel!.text = tag;
    }

    private bool TryBuildLabel(string tag)
    {
        if (!WorldLabelRuntime.TryGetNativeWoodenSign(out Sign donor) ||
            !PortalSignVisualFactory.TryCreate(
                portal,
                donor,
                tag,
                out PortalSignVisual visual))
        {
            return false;
        }

        labelRoot = visual.Root;
        frontLabel = visual.FrontLabel;
        backLabel = visual.BackLabel;
        frontGlowMaterial = visual.FrontGlowMaterial;
        backGlowMaterial = visual.BackGlowMaterial;
        currentTag = tag;
        WorldLabelRuntime.LogPortalLabelCreated(portal);
        return true;
    }

    private void OnDestroy()
    {
        DisposeVisual();
        WorldLabelRuntime.Forget(this);
    }

    private void DisposeVisual()
    {
        if (labelRoot != null)
        {
            Destroy(labelRoot);
            labelRoot = null;
            frontLabel = null;
            backLabel = null;
        }

        if (frontGlowMaterial != null)
        {
            Destroy(frontGlowMaterial);
            frontGlowMaterial = null;
        }

        if (backGlowMaterial != null)
        {
            Destroy(backGlowMaterial);
            backGlowMaterial = null;
        }
    }
}
