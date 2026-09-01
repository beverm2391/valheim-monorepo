using TMPro;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal sealed class PortalLabelController : MonoBehaviour
{
    private const float LabelClearanceMeters = 0.35f;
    private const float CanvasScale = 0.005f;

    private TeleportWorld portal = null!;
    private GameObject? labelRoot;
    private Canvas? labelCanvas;
    private TextMeshProUGUI? label;
    private string? currentTag;
    private bool disposed;

    internal void Initialize(TeleportWorld source)
    {
        portal = source;
        InvokeRepeating(
            nameof(Refresh),
            0f,
            WorldLabelVisibility.PortalRefreshIntervalSeconds);
    }

    internal void DisposeAndRemove()
    {
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

        if (label == null && !TryBuildLabel())
        {
            return;
        }

        string tag = portal.GetText();
        if (tag != currentTag)
        {
            currentTag = tag;
            label!.text = tag;
        }

        Player? viewer = Player.m_localPlayer;
        Camera? camera = Utils.GetMainCamera();
        bool hasViewer = viewer != null && camera != null;
        float distanceSquared = hasViewer
            ? (viewer!.transform.position - portal.transform.position).sqrMagnitude
            : float.PositiveInfinity;
        bool hasLineOfSight = hasViewer && HasLineOfSight(camera!);
        bool visible = WorldLabelVisibility.ShouldShowPortalTag(
            tag,
            hasViewer,
            distanceSquared,
            hasLineOfSight);

        if (labelRoot!.activeSelf != visible)
        {
            labelRoot.SetActive(visible);
        }

        if (visible)
        {
            labelCanvas!.worldCamera = camera;
        }
    }

    private bool TryBuildLabel()
    {
        if (!WorldLabelRuntime.TryGetNativeTextDonor(out NativeTextDonor donor))
        {
            return false;
        }

        labelRoot = new GameObject(
            "Benheim Portal Tag Label",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(Billboard));
        labelRoot.hideFlags = HideFlags.DontSave;

        float top = portal.m_model != null
            ? portal.m_model.bounds.max.y
            : portal.transform.position.y + 3.5f;
        labelRoot.transform.position = new Vector3(
            portal.transform.position.x,
            top + LabelClearanceMeters,
            portal.transform.position.z);
        labelRoot.transform.SetParent(portal.transform, worldPositionStays: true);

        RectTransform rootRect = (RectTransform)labelRoot.transform;
        rootRect.sizeDelta = new Vector2(640f, 110f);
        rootRect.localScale = Vector3.one * CanvasScale;

        labelCanvas = labelRoot.GetComponent<Canvas>()!;
        labelCanvas.renderMode = RenderMode.WorldSpace;

        Billboard billboard = labelRoot.GetComponent<Billboard>()!;
        billboard.m_vertical = true;
        billboard.m_invert = true;

        label = labelRoot.AddComponent<TextMeshProUGUI>();
        label.font = donor.Font;
        label.fontSharedMaterial = donor.Material;
        label.color = WorldLabelStyle.PortalAmber;
        label.fontSize = 64f;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.richText = false;
        label.raycastTarget = false;
        labelRoot.SetActive(false);
        WorldLabelRuntime.LogPortalLabelCreated(portal);
        return true;
    }

    private bool HasLineOfSight(Camera camera)
    {
        Vector3 target = labelRoot!.transform.position;
        if (!Physics.Linecast(
                camera.transform.position,
                target,
                out RaycastHit hit,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        Transform? hitTransform = hit.transform;
        return hitTransform != null &&
            (hitTransform == portal.transform || hitTransform.IsChildOf(portal.transform));
    }

    private void OnDestroy()
    {
        DisposeVisual();
        WorldLabelRuntime.Forget(this);
    }

    private void DisposeVisual()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelInvoke(nameof(Refresh));
        if (labelRoot != null)
        {
            Destroy(labelRoot);
            labelRoot = null;
            labelCanvas = null;
            label = null;
        }
    }
}
