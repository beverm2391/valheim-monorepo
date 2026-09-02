using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace BenheimQoL.WorldLabels;

internal static class PortalSignVisualFactory
{
    internal const float RefreshIntervalSeconds = 0.5f;
    internal const float BoardClearanceMeters = 0.25f;

    internal static bool HasUsableVisual(Sign sign)
    {
        if (sign == null)
        {
            return false;
        }

        TextMeshProUGUI? widget = sign.m_textWidget;
        return widget != null &&
            widget.font != null &&
            widget.fontSharedMaterial != null &&
            sign.GetComponentsInChildren<MeshRenderer>(includeInactive: true).Length > 0;
    }

    internal static bool TryCreate(
        TeleportWorld portal,
        Sign donor,
        string tag,
        out PortalSignVisual visual)
    {
        visual = default;
        if (!HasUsableVisual(donor))
        {
            return false;
        }

        float portalCrown = ResolvePortalCrown(portal);

        GameObject root = new("Benheim Portal Sign Board")
        {
            hideFlags = HideFlags.DontSave,
        };
        root.transform.SetParent(portal.transform, worldPositionStays: false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        int rendererCount = CopyRenderHierarchy(donor.transform, root.transform);
        if (rendererCount == 0 ||
            !TryCreateFace(root.transform, donor, tag, backFace: false,
                out TextMeshProUGUI front, out Material frontGlow) ||
            !TryCreateFace(root.transform, donor, tag, backFace: true,
                out TextMeshProUGUI back, out Material backGlow))
        {
            Object.Destroy(root);
            return false;
        }

        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
        Bounds boardBounds = renderers[0].bounds;
        for (int index = 1; index < renderers.Length; index++)
        {
            boardBounds.Encapsulate(renderers[index].bounds);
        }

        root.transform.position += Vector3.up *
            (portalCrown + BoardClearanceMeters - boardBounds.min.y);

        visual = new PortalSignVisual(root, front, back, frontGlow, backGlow);
        return true;
    }

    private static int CopyRenderHierarchy(Transform source, Transform target)
    {
        int rendererCount = CopyMesh(source.gameObject, target.gameObject);
        for (int index = 0; index < source.childCount; index++)
        {
            Transform sourceChild = source.GetChild(index);
            GameObject targetChildObject = new(sourceChild.gameObject.name);
            targetChildObject.SetActive(sourceChild.gameObject.activeSelf);
            Transform targetChild = targetChildObject.transform;
            targetChild.SetParent(target, worldPositionStays: false);
            targetChild.localPosition = sourceChild.localPosition;
            targetChild.localRotation = sourceChild.localRotation;
            targetChild.localScale = sourceChild.localScale;
            rendererCount += CopyRenderHierarchy(sourceChild, targetChild);
        }

        return rendererCount;
    }

    private static int CopyMesh(GameObject source, GameObject target)
    {
        MeshFilter? sourceFilter = source.GetComponent<MeshFilter>();
        MeshRenderer? sourceRenderer = source.GetComponent<MeshRenderer>();
        if (sourceFilter == null || sourceRenderer == null || sourceFilter.sharedMesh == null)
        {
            return 0;
        }

        target.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
        MeshRenderer renderer = target.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = sourceRenderer.sharedMaterials;
        renderer.enabled = sourceRenderer.enabled;
        renderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
        renderer.receiveShadows = sourceRenderer.receiveShadows;
        renderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
        renderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
        renderer.motionVectorGenerationMode = sourceRenderer.motionVectorGenerationMode;
        renderer.allowOcclusionWhenDynamic = sourceRenderer.allowOcclusionWhenDynamic;
        return 1;
    }

    private static float ResolvePortalCrown(TeleportWorld portal)
    {
        // TeleportWorld.m_model is the emissive connection model, not a
        // contract for either portal's complete physical silhouette. The
        // highest active native mesh is the visual crown players actually see.
        float crown = float.NegativeInfinity;
        foreach (MeshRenderer renderer in
                 portal.GetComponentsInChildren<MeshRenderer>(includeInactive: false))
        {
            if (renderer.enabled)
            {
                crown = Mathf.Max(crown, renderer.bounds.max.y);
            }
        }

        return float.IsNegativeInfinity(crown)
            ? portal.transform.position.y + 3.5f
            : crown;
    }

    private static bool TryCreateFace(
        Transform root,
        Sign donor,
        string tag,
        bool backFace,
        out TextMeshProUGUI label,
        out Material glowMaterial)
    {
        label = null!;
        glowMaterial = null!;
        TextMeshProUGUI source = donor.m_textWidget;
        if (source.font == null || source.fontSharedMaterial == null)
        {
            return false;
        }

        GameObject face = new(
            backFace ? "Back Text" : "Front Text",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        face.hideFlags = HideFlags.DontSave;
        RectTransform rect = (RectTransform)face.transform;
        rect.SetParent(root, worldPositionStays: false);

        Vector3 localPosition = donor.transform.InverseTransformPoint(source.transform.position);
        Quaternion localRotation = Quaternion.Inverse(donor.transform.rotation) *
            source.transform.rotation;
        Vector3 localScale = Divide(source.transform.lossyScale, donor.transform.lossyScale);
        Quaternion faceTurn = backFace
            ? Quaternion.Euler(0f, 180f, 0f)
            : Quaternion.identity;
        rect.localPosition = faceTurn * localPosition;
        rect.localRotation = faceTurn * localRotation;
        rect.localScale = localScale;

        RectTransform sourceRect = source.rectTransform;
        rect.sizeDelta = sourceRect.rect.size;
        rect.pivot = sourceRect.pivot;

        Canvas canvas = face.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        Canvas? sourceCanvas = source.GetComponentInParent<Canvas>();
        if (sourceCanvas != null)
        {
            canvas.sortingLayerID = sourceCanvas.sortingLayerID;
            canvas.sortingOrder = sourceCanvas.sortingOrder;
        }

        label = face.GetComponent<TextMeshProUGUI>();
        label.font = source.font;
        label.fontSharedMaterial = source.fontSharedMaterial;
        label.fontSize = source.fontSize;
        label.fontStyle = source.fontStyle;
        label.alignment = source.alignment;
        label.textWrappingMode = source.textWrappingMode;
        label.overflowMode = source.overflowMode;
        label.margin = source.margin;
        label.characterSpacing = source.characterSpacing;
        label.wordSpacing = source.wordSpacing;
        label.lineSpacing = source.lineSpacing;
        label.paragraphSpacing = source.paragraphSpacing;
        label.color = WorldLabelStyle.PortalAmber;
        label.richText = false;
        label.raycastTarget = false;
        label.text = tag;

        glowMaterial = WorldLabelStyle.CreateSignLetterMaterial(label);
        label.fontSharedMaterial = glowMaterial;
        return true;
    }

    private static Vector3 Divide(Vector3 value, Vector3 divisor) => new(
        divisor.x == 0f ? value.x : value.x / divisor.x,
        divisor.y == 0f ? value.y : value.y / divisor.y,
        divisor.z == 0f ? value.z : value.z / divisor.z);
}

internal readonly struct PortalSignVisual
{
    internal PortalSignVisual(
        GameObject root,
        TextMeshProUGUI frontLabel,
        TextMeshProUGUI backLabel,
        Material frontGlowMaterial,
        Material backGlowMaterial)
    {
        Root = root;
        FrontLabel = frontLabel;
        BackLabel = backLabel;
        FrontGlowMaterial = frontGlowMaterial;
        BackGlowMaterial = backGlowMaterial;
    }

    internal GameObject Root { get; }
    internal TextMeshProUGUI FrontLabel { get; }
    internal TextMeshProUGUI BackLabel { get; }
    internal Material FrontGlowMaterial { get; }
    internal Material BackGlowMaterial { get; }
}
