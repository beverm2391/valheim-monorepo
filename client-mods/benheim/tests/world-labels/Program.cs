using System;
using System.Linq;
using System.Reflection;
using BenheimQoL.WorldLabels;
using TMPro;
using UnityEngine;

internal static class Program
{
    private static void Main()
    {
        VerifyNativeDonorBoundary();
        VerifyPortalBoardLifecycle();
        Console.WriteLine("World Label runtime-shaped sign-board contract checks passed");
    }

    private static void VerifyNativeDonorBoundary()
    {
        WorldLabelRuntime.Reset();
        Plugin.Log.Clear();
        ZNetScene.instance = new ZNetScene();

        Sign decorativeSign = CreateNativeSign("piece_sign_darkwood");
        ZNetScene.instance.m_prefabs.Add(decorativeSign.gameObject);
        Assert(!WorldLabelRuntime.TryGetNativeWoodenSign(out _),
            "a different Sign prefab must not become the wooden-board donor");

        Sign woodenSign = CreateNativeSign("piece_sign");
        ZNetScene.instance.m_prefabs.Add(woodenSign.gameObject);
        Assert(WorldLabelRuntime.TryGetNativeWoodenSign(out Sign resolved) &&
            ReferenceEquals(resolved, woodenSign),
            "the runtime must resolve the exact native piece_sign donor");
        Assert(Plugin.Log.Infos.Count(message =>
                message.Contains("piece_sign visual", StringComparison.Ordinal)) == 1,
            "native donor resolution must be logged once");
    }

    private static void VerifyPortalBoardLifecycle()
    {
        WorldLabelRuntime.Reset();
        Plugin.Log.Clear();
        ZNetScene.instance = new ZNetScene();

        TeleportWorld portal = CreatePortal("portal_wood", string.Empty);
        WorldLabelRuntime.Attach(portal);
        WorldLabelRuntime.Attach(portal);
        PortalLabelController controller =
            portal.GetComponent<PortalLabelController>()
            ?? throw new InvalidOperationException("portal controller was not attached");

        InvokeRefresh(controller);
        Assert(GetRoot(controller) == null,
            "an empty portal tag must create no board");
        Assert(Plugin.Log.Warnings.Count == 0,
            "an empty tag must not warn while the native donor is unavailable");

        portal.Tag = "home";
        InvokeRefresh(controller);
        InvokeRefresh(controller);
        Assert(GetRoot(controller) == null && Plugin.Log.Warnings.Count == 1,
            "a non-empty tag must wait for piece_sign and log that boundary once");

        Sign donor = CreateNativeSign("piece_sign");
        ZNetScene.instance.m_prefabs.Add(donor.gameObject);
        InvokeRefresh(controller);

        GameObject root = GetRoot(controller)
            ?? throw new InvalidOperationException("native sign board was not created");
        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        Assert(labels.Length == 2 && labels.All(label => label.text == "home"),
            "the exact portal tag must appear on both sides");
        Assert(labels.All(label => !label.richText && !label.raycastTarget),
            "portal tags must remain literal and non-interactive");
        Assert(labels.All(label => label.color == WorldLabelStyle.PortalAmber &&
            label.fontSharedMaterial!.name == "Benheim Sign Letter Glow"),
            "both faces must use the existing glowing sign-letter treatment");
        Assert(root.GetComponentsInChildren<MeshRenderer>(true).Length == 2,
            "the visual must copy every mesh in the native board hierarchy");
        Assert(root.GetComponentsInChildren<Sign>(true).Length == 0 &&
            root.GetComponentsInChildren<ZNetView>(true).Length == 0 &&
            root.GetComponentsInChildren<Collider>(true).Length == 0,
            "the visual must not copy sign, network, or collider behavior");
        Assert(root.hideFlags == HideFlags.DontSave &&
            ReferenceEquals(root.transform.parent, portal.transform),
            "the board must be unsaved and anchored directly to the portal");
        Assert(root.transform.localRotation == Quaternion.identity,
            "the board must inherit portal rotation instead of billboarding");

        MeshRenderer board = root.GetComponentsInChildren<MeshRenderer>(true)[0];
        Assert(Approximately(
                board.bounds.min.y,
                4f + PortalSignVisualFactory.BoardClearanceMeters),
            "the board bottom must sit 25 centimeters above the full visual crown, not m_model");
        Assert(labels.Select(label => label.transform.localRotation.yDegrees)
                .OrderBy(value => value)
                .SequenceEqual(new[] { 0f, 180f }),
            "the two text faces must point in opposite portal-relative directions");

        GameObject originalRoot = root;
        portal.Tag = "<b>exact</b>";
        InvokeRefresh(controller);
        Assert(ReferenceEquals(originalRoot, GetRoot(controller)) &&
            labels.All(label => label.text == "<b>exact</b>"),
            "renaming must update both existing faces exactly without duplication");

        Material[] glowMaterials = labels.Select(label => label.fontSharedMaterial!).ToArray();
        portal.Tag = string.Empty;
        InvokeRefresh(controller);
        Assert(originalRoot.Destroyed && GetRoot(controller) == null,
            "clearing a portal tag must remove the whole board");
        Assert(glowMaterials.All(material => material.Destroyed),
            "clearing a portal tag must release both cloned glow materials");

        portal.Tag = "stone";
        portal.gameObject.name = "portal_stone";
        InvokeRefresh(controller);
        GameObject stoneRoot = GetRoot(controller)
            ?? throw new InvalidOperationException("stone portal board was not recreated");
        Assert(stoneRoot.GetComponentsInChildren<TextMeshProUGUI>(true)
                .All(label => label.text == "stone"),
            "the same visual must support a stone portal tag");

        WorldLabelRuntime.Reset();
        Assert(stoneRoot.Destroyed && controller.Destroyed,
            "runtime reset must clean up the portal visual and controller");
    }

    private static Sign CreateNativeSign(string name)
    {
        GameObject root = new(name);
        Sign sign = root.AddComponent<Sign>();
        MeshFilter filter = root.AddComponent<MeshFilter>();
        filter.sharedMesh = new Mesh
        {
            bounds = new Bounds(
                new Vector3(-0.6f, -0.25f, -0.05f),
                new Vector3(0.6f, 0.25f, 0.05f)),
        };
        MeshRenderer renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { new Material { name = "Native Sign Wood" } };
        GameObject trim = new("Board Trim");
        trim.transform.SetParent(root.transform, worldPositionStays: false);
        trim.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        trim.AddComponent<MeshFilter>().sharedMesh = new Mesh
        {
            bounds = new Bounds(
                new Vector3(-0.6f, -0.04f, -0.06f),
                new Vector3(0.6f, 0.04f, 0.06f)),
        };
        trim.AddComponent<MeshRenderer>().sharedMaterials =
            new[] { new Material { name = "Native Sign Trim" } };

        GameObject textObject = new(
            "Text",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, worldPositionStays: false);
        textObject.transform.localPosition = new Vector3(0f, 0f, 0.06f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = new TMP_FontAsset();
        text.fontSharedMaterial = new Material { name = "Native Sign Text" };
        text.fontSize = 2f;
        text.color = Color.white;
        ((RectTransform)textObject.transform).sizeDelta = new Vector2(1.1f, 0.4f);
        sign.m_textWidget = text;
        return sign;
    }

    private static TeleportWorld CreatePortal(string name, string tag)
    {
        GameObject gameObject = new(name);
        TeleportWorld portal = gameObject.AddComponent<TeleportWorld>();
        portal.Tag = tag;
        portal.m_model = gameObject.AddComponent<MeshRenderer>();
        portal.m_model.ExplicitBounds = new Bounds(
            new Vector3(-1f, 0f, -0.5f),
            new Vector3(1f, 2f, 0.5f));
        GameObject crown = new("Portal Crown");
        crown.transform.SetParent(gameObject.transform, worldPositionStays: false);
        crown.AddComponent<MeshRenderer>().ExplicitBounds = new Bounds(
            new Vector3(-1f, 2f, -0.5f),
            new Vector3(1f, 4f, 0.5f));
        return portal;
    }

    private static void InvokeRefresh(PortalLabelController controller) =>
        typeof(PortalLabelController)
            .GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(controller, null);

    private static GameObject? GetRoot(PortalLabelController controller) =>
        (GameObject?)typeof(PortalLabelController)
            .GetField("labelRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller);

    private static bool Approximately(float left, float right) =>
        MathF.Abs(left - right) < 0.0001f;

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
