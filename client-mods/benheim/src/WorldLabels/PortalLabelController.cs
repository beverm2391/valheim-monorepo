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
    private readonly PortalLabelDiagnostics diagnostics = new();

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
            diagnostics.Observe(portal, tag, "empty_tag");
            return;
        }

        if (labelRoot == null && !TryBuildLabel(tag))
        {
            return;
        }

        if (tag == currentTag)
        {
            ObserveLayout(tag, refreshMesh: false);
            return;
        }

        currentTag = tag;
        if (frontLabel != null) frontLabel.text = tag;
        if (backLabel != null) backLabel.text = tag;
        ObserveLayout(tag, refreshMesh: true);
    }

    private bool TryBuildLabel(string tag)
    {
        if (!WorldLabelRuntime.TryGetNativeWoodenSign(out Sign donor))
        {
            diagnostics.Observe(portal, tag, WorldLabelRuntime.NativeSignPendingReason);
            return false;
        }
        if (!PortalSignVisualFactory.TryCreate(
                portal,
                donor,
                tag,
                out PortalSignVisual visual))
        {
            diagnostics.Observe(portal, tag, "visual_creation_failed");
            return false;
        }

        labelRoot = visual.Root;
        frontLabel = visual.FrontLabel;
        backLabel = visual.BackLabel;
        frontGlowMaterial = visual.FrontGlowMaterial;
        backGlowMaterial = visual.BackGlowMaterial;
        currentTag = tag;
        ObserveLayout(tag, refreshMesh: true);
        return true;
    }

    private void ObserveLayout(string tag, bool refreshMesh)
    {
        string state = labelRoot == null ? "missing_board"
            : labelRoot.GetComponentsInChildren<MeshRenderer>(includeInactive: true).Length == 0
                ? "missing_board_mesh" : "visible";
        diagnostics.Observe(portal, tag, state, frontLabel, backLabel, refreshMesh);
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
