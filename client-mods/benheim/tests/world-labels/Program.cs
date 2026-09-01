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
        VerifyVisibilityPolicy();
        VerifyRuntimeLifecycle();
        Console.WriteLine("World Label runtime and behavior checks passed");
    }

    private static void VerifyVisibilityPolicy()
    {
        Assert(!WorldLabelVisibility.ShouldShowPortalTag("", true, 0f, true),
            "empty tags must stay hidden");
        Assert(!WorldLabelVisibility.ShouldShowPortalTag(null, true, 0f, true),
            "missing tags must stay hidden");
        Assert(!WorldLabelVisibility.ShouldShowPortalTag("home", false, 0f, true),
            "labels require a local viewer");
        Assert(!WorldLabelVisibility.ShouldShowPortalTag("home", true, 1f, false),
            "walls must hide labels");
        Assert(WorldLabelVisibility.ShouldShowPortalTag("home", true, 30f * 30f, true),
            "the 30-meter boundary must remain visible");
        Assert(!WorldLabelVisibility.ShouldShowPortalTag("home", true, 30.01f * 30.01f, true),
            "labels beyond 30 meters must stay hidden");
        Assert(WorldLabelVisibility.ShouldShowPortalTag("<b>exact</b>", true, 1f, true),
            "visibility policy must not interpret or rewrite tag text");
    }

    private static void VerifyRuntimeLifecycle()
    {
        WorldLabelRuntime.Reset();
        Plugin.Log.Clear();
        ZNetScene.instance = null;
        Player.m_localPlayer = CreatePlayer(Vector3.zero);
        Utils.MainCamera = CreateCamera(Vector3.zero);

        TeleportWorld portal = CreatePortal(new Vector3(0f, 0f, 30f), "home");
        WorldLabelRuntime.Attach(portal);
        WorldLabelRuntime.Attach(portal);
        PortalLabelController controller =
            portal.GetComponent<PortalLabelController>()
            ?? throw new InvalidOperationException("portal controller was not attached");

        InvokeRefresh(controller);
        InvokeRefresh(controller);
        Assert(GetLabelRoot(controller) == null,
            "a portal must wait when the native donor lifecycle is not ready");
        Assert(Plugin.Log.Warnings.Count == 1 &&
            Plugin.Log.Warnings[0].Contains("ZNetScene is not ready", StringComparison.Ordinal),
            "the pending boundary must be readable and logged once");

        ZNetScene missingDonorScene = new ZNetScene();
        ZNetScene.instance = missingDonorScene;
        GameObject unrelatedPrefab = new GameObject("unrelated");
        missingDonorScene.m_prefabs.Add(unrelatedPrefab);
        InvokeRefresh(controller);
        InvokeRefresh(controller);
        Assert(unrelatedPrefab.GetComponentLookupCount<Sign>() == 1,
            "one ZNetScene without a donor must be scanned only once");

        ZNetScene readyScene = new ZNetScene();
        ZNetScene.instance = readyScene;
        readyScene.m_prefabs.Add(CreateSignPrefab("sign_wood"));

        InvokeRefresh(controller);
        GameObject labelRoot = GetLabelRoot(controller)
            ?? throw new InvalidOperationException("registered native Sign did not create a label");
        TextMeshProUGUI label = GetLabel(controller)
            ?? throw new InvalidOperationException("created label has no native text component");
        Assert(label.text == "home", "the initial portal tag must be copied exactly");
        Assert(labelRoot.activeSelf, "the unobstructed 30-meter label must be visible");
        Assert(Plugin.Log.Infos.Any(message =>
                message.Contains("registered Sign prefab 'sign_wood'", StringComparison.Ordinal)),
            "donor resolution must identify the actual registered native Sign");
        Assert(Plugin.Log.Infos.Count(message =>
                message.Contains("Portal label created", StringComparison.Ordinal)) == 1,
            "one controller must create exactly one label");

        TeleportWorld secondPortal = CreatePortal(new Vector3(0f, 0f, 10f), "second");
        WorldLabelRuntime.Attach(secondPortal);
        PortalLabelController secondController =
            secondPortal.GetComponent<PortalLabelController>()
            ?? throw new InvalidOperationException("second portal controller was not attached");
        InvokeRefresh(secondController);
        Assert(GetLabelRoot(secondController) != null,
            "a second portal must create its own label from the cached donor");
        Assert(Plugin.Log.Infos.Count(message =>
                message.Contains("Portal label created", StringComparison.Ordinal)) == 1,
            "label creation evidence must remain one-shot across portal instances");

        portal.Tag = "<b>exact</b>";
        InvokeRefresh(controller);
        Assert(label.text == "<b>exact</b>", "renamed portal tags must update exactly");
        Assert(!label.richText, "portal tags must remain literal text");

        portal.Tag = string.Empty;
        InvokeRefresh(controller);
        Assert(!labelRoot.activeSelf, "empty portal tags must hide the label");

        portal.Tag = "home";
        portal.transform.position = new Vector3(0f, 0f, 30.01f);
        InvokeRefresh(controller);
        Assert(!labelRoot.activeSelf, "labels beyond 30 meters must stay hidden");

        portal.transform.position = new Vector3(0f, 0f, 30f);
        Physics.NextLinecastHit = true;
        Physics.NextHitTransform = new GameObject("wall").transform;
        InvokeRefresh(controller, preserveLinecast: true);
        Assert(!labelRoot.activeSelf, "an occluding wall must hide the label");

        Transform portalChild = new GameObject("portal collider").transform;
        portalChild.SetParent(portal.transform, worldPositionStays: true);
        Physics.NextLinecastHit = true;
        Physics.NextHitTransform = portalChild;
        InvokeRefresh(controller, preserveLinecast: true);
        Assert(labelRoot.activeSelf, "the portal's own collider must not hide its label");

        InvokeRefresh(controller);
        Assert(ReferenceEquals(labelRoot, GetLabelRoot(controller)),
            "refresh must reuse the existing label instead of duplicating it");
        Assert(Plugin.Log.Infos.Count(message =>
                message.Contains("Portal label created", StringComparison.Ordinal)) == 1,
            "refresh must not log or create a duplicate label");

        WorldLabelRuntime.Reset();
        Assert(labelRoot.Destroyed, "runtime reset must destroy the unsaved label visual");
        Assert(controller.Destroyed, "runtime reset must remove the portal controller");

        Plugin.Log.Clear();
        ZNetScene.instance = null;
        TeleportWorld retryPortal = CreatePortal(Vector3.zero, "retry");
        WorldLabelRuntime.Attach(retryPortal);
        PortalLabelController retryController =
            retryPortal.GetComponent<PortalLabelController>()
            ?? throw new InvalidOperationException("retry controller was not attached");
        InvokeRefresh(retryController);

        Sign loadedSign = CreateSignPrefab("loaded sign").GetComponent<Sign>()!;
        Material originalMaterial = loadedSign.m_textWidget.fontSharedMaterial!;
        WorldLabelRuntime.Attach(loadedSign);
        Material glowMaterial = loadedSign.m_textWidget.fontSharedMaterial!;
        Assert(!ReferenceEquals(originalMaterial, glowMaterial),
            "the runtime-shaped Sign Glow stub must replace the widget material");
        InvokeRefresh(retryController);
        TextMeshProUGUI retryLabel = GetLabel(retryController)
            ?? throw new InvalidOperationException("retrying portal did not create a label");
        Assert(GetLabelRoot(retryController) != null,
            "a later loaded Sign must unblock the existing retrying portal");
        Assert(ReferenceEquals(retryLabel.fontSharedMaterial, originalMaterial),
            "the portal must snapshot the native material before Sign Glow mutates the widget");
        Assert(Plugin.Log.Infos.Any(message =>
                message.Contains("loaded Sign 'loaded sign'", StringComparison.Ordinal)),
            "loaded Sign resolution must be readable");
        loadedSign.GetComponent<SignGlowController>()!.RestoreAndRemove();
        Assert(glowMaterial.Destroyed && !originalMaterial.Destroyed,
            "Sign Glow cleanup must destroy only its cloned material");
        Assert(ReferenceEquals(retryLabel.fontSharedMaterial, originalMaterial),
            "a streamed-out donor sign must not invalidate an existing portal label");
        WorldLabelRuntime.Reset();
    }

    private static TeleportWorld CreatePortal(Vector3 position, string tag)
    {
        GameObject gameObject = new GameObject("portal_wood");
        gameObject.transform.position = position;
        TeleportWorld portal = gameObject.AddComponent<TeleportWorld>();
        portal.Tag = tag;
        return portal;
    }

    private static GameObject CreateSignPrefab(string name)
    {
        GameObject gameObject = new GameObject(name);
        Sign sign = gameObject.AddComponent<Sign>();
        sign.m_textWidget = new TextMeshProUGUI
        {
            font = new TMP_FontAsset(),
            fontSharedMaterial = new Material()
        };
        return gameObject;
    }

    private static Player CreatePlayer(Vector3 position)
    {
        GameObject gameObject = new GameObject("player");
        gameObject.transform.position = position;
        return gameObject.AddComponent<Player>();
    }

    private static Camera CreateCamera(Vector3 position)
    {
        GameObject gameObject = new GameObject("camera");
        gameObject.transform.position = position;
        return gameObject.AddComponent<Camera>();
    }

    private static void InvokeRefresh(
        PortalLabelController controller,
        bool preserveLinecast = false)
    {
        if (!preserveLinecast)
        {
            Physics.NextLinecastHit = false;
            Physics.NextHitTransform = null;
        }

        typeof(PortalLabelController)
            .GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(controller, null);
    }

    private static GameObject? GetLabelRoot(PortalLabelController controller) =>
        (GameObject?)typeof(PortalLabelController)
            .GetField("labelRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller);

    private static TextMeshProUGUI? GetLabel(PortalLabelController controller) =>
        (TextMeshProUGUI?)typeof(PortalLabelController)
            .GetField("label", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller);

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
