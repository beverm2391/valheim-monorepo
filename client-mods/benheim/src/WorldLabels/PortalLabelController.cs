using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;

namespace BenheimQoL.WorldLabels;

internal sealed class PortalLabelController : MonoBehaviour
{
    private const float LabelClearanceMeters = 0.35f;

    private TeleportWorld portal = null!;
    private GameObject? labelRoot;
    private TMP_Text? label;
    private Vector3 labelWorldPosition;
    private string? currentTag;
    private bool visibleByPolicy;
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
            WorldLabelRuntime.LogPortalPresentationPending();
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

        visibleByPolicy = visible;
        UpdatePlacement(camera);
    }

    private bool TryBuildLabel()
    {
        float top = portal.m_model != null
            ? portal.m_model.bounds.max.y
            : portal.transform.position.y + 3.5f;
        labelWorldPosition = new Vector3(
            portal.transform.position.x,
            top + LabelClearanceMeters,
            portal.transform.position.z);
        if (!WorldFeedback.TryCreatePersistentBonusText(
                labelWorldPosition,
                out GameObject createdRoot,
                out TMP_Text createdText))
        {
            return false;
        }

        labelRoot = createdRoot;
        label = createdText;
        WorldLabelRuntime.LogPortalLabelCreated(portal);
        return true;
    }

    private void LateUpdate()
    {
        if (!disposed && labelRoot != null && visibleByPolicy)
        {
            UpdatePlacement(Utils.GetMainCamera());
        }
    }

    private void UpdatePlacement(Camera? camera)
    {
        bool visible = visibleByPolicy &&
            camera != null &&
            WorldFeedback.PlacePersistentText(labelRoot!, labelWorldPosition, camera);
        if (labelRoot!.activeSelf != visible)
        {
            labelRoot.SetActive(visible);
        }
    }

    private bool HasLineOfSight(Camera camera)
    {
        Vector3 target = labelWorldPosition;
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
            label = null;
        }
    }
}
