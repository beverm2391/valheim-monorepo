using System;
using System.Linq;
using System.Reflection;
using BenheimQoL.Infrastructure;
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
        DamageText.instance = null!;
        Player.m_localPlayer = CreatePlayer(Vector3.zero);
        Camera camera = CreateCamera(Vector3.zero);
        Utils.MainCamera = camera;

        TeleportWorld portal = CreatePortal(new Vector3(0f, 0f, 30f), "home");
        WorldLabelRuntime.Attach(portal);
        WorldLabelRuntime.Attach(portal);
        PortalLabelController controller =
            portal.GetComponent<PortalLabelController>()
            ?? throw new InvalidOperationException("portal controller was not attached");

        InvokeRefresh(controller);
        InvokeRefresh(controller);
        Assert(GetLabelRoot(controller) == null,
            "a portal must wait when native Bonus world text is not ready");
        Assert(Plugin.Log.Warnings.Count == 1 &&
            Plugin.Log.Warnings[0].Contains("Bonus world-text presentation", StringComparison.Ordinal),
            "the pending boundary must be readable and logged once");

        TMP_FontAsset nativeFont = new TMP_FontAsset();
        Material nativeMaterial = new Material();
        DamageText damageText = CreateDamageText(nativeFont, nativeMaterial);
        DamageText.instance = damageText;

        UnityEngine.Random.state = new UnityEngine.Random.State(1207);
        InvokeRefresh(controller);
        Assert(UnityEngine.Random.state.Value == 1207,
            "persistent portal-label creation must preserve Unity's shared random state");
        GameObject labelRoot = GetLabelRoot(controller)
            ?? throw new InvalidOperationException("native Bonus world text did not create a label");
        TMP_Text label = GetLabel(controller)
            ?? throw new InvalidOperationException("created label has no TMP text component");
        Assert(label.text == "home", "the initial portal tag must be copied exactly");
        Assert(labelRoot.activeSelf, "the unobstructed 30-meter label must be visible");
        Assert(ReferenceEquals(label.font, nativeFont) &&
            ReferenceEquals(label.fontSharedMaterial, nativeMaterial) &&
            label.StyleMarker == "native-high-contrast",
            "portal labels must retain the exact native overlay font, material, and treatment");
        Assert(label.color.r == 1f && label.color.g == 0.63f &&
            label.color.b == 0.24f && label.color.a == 1f && label.fontSize == 24f,
            "portal labels must use native Bonus color and large-font sizing");
        Assert(damageText.ActiveWorldTextCount == 0,
            "persistent labels must detach from native transient animation and lifetime tracking");
        Assert(ReferenceEquals(labelRoot.transform.parent, damageText.transform),
            "persistent labels must stay in the existing native world-text overlay hierarchy");
        Assert(labelRoot.transform.position.x == camera.ScreenPoint.x &&
            labelRoot.transform.position.y == camera.ScreenPoint.y,
            "the portal's world anchor must be projected into the native overlay");
        Assert(Plugin.Log.Infos.Count(message =>
                message.Contains("Portal label created", StringComparison.Ordinal)) == 1,
            "one controller must create exactly one label");

        TeleportWorld secondPortal = CreatePortal(new Vector3(0f, 0f, 10f), "second");
        WorldLabelRuntime.Attach(secondPortal);
        PortalLabelController secondController =
            secondPortal.GetComponent<PortalLabelController>()
            ?? throw new InvalidOperationException("second portal controller was not attached");
        UnityEngine.Random.state = new UnityEngine.Random.State(4815);
        InvokeRefresh(secondController);
        Assert(UnityEngine.Random.state.Value == 4815,
            "each later portal-label creation must preserve Unity's shared random state");
        Assert(GetLabelRoot(secondController) != null,
            "a second portal must create its own persistent Bonus overlay");
        Assert(damageText.CreatedBonusTextCount == 2 && damageText.ActiveWorldTextCount == 0,
            "each portal must own one detached Bonus overlay and no transient entry");
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

        camera.ScreenPoint = new Vector3(700f, 300f, 1f);
        InvokeLateUpdate(controller);
        Assert(labelRoot.activeSelf &&
            labelRoot.transform.position.x == 700f &&
            labelRoot.transform.position.y == 300f,
            "the stationary world anchor must follow camera projection without transient motion");

        camera.ScreenPoint = new Vector3(700f, 300f, -1f);
        InvokeLateUpdate(controller);
        Assert(!labelRoot.activeSelf, "labels behind the camera must stay hidden");
        camera.ScreenPoint = new Vector3(700f, 300f, 1f);
        InvokeRefresh(controller);
        Assert(labelRoot.activeSelf, "an on-screen visible label must reappear");

        InvokeRefresh(controller);
        Assert(ReferenceEquals(labelRoot, GetLabelRoot(controller)),
            "refresh must reuse the existing label instead of duplicating it");
        Assert(Plugin.Log.Infos.Count(message =>
                message.Contains("Portal label created", StringComparison.Ordinal)) == 1,
            "refresh must not log or create a duplicate label");

        WorldFeedback.ShowAbovePlayer(Player.m_localPlayer!, "Perfect parry +10");
        Assert(damageText.ActiveWorldTextCount == 1 &&
            damageText.LastWorldText == "Perfect parry +10" &&
            damageText.LastWorldTextDuration == 3f,
            "the shared refactor must preserve existing transient Perfect Parry feedback");

        WorldLabelRuntime.Reset();
        Assert(labelRoot.Destroyed, "runtime reset must destroy the unsaved label visual");
        Assert(controller.Destroyed, "runtime reset must remove the portal controller");

        Plugin.Log.Clear();
        Sign loadedSign = CreateSignPrefab("loaded sign").GetComponent<Sign>()!;
        Material originalMaterial = loadedSign.m_textWidget.fontSharedMaterial!;
        WorldLabelRuntime.Attach(loadedSign);
        Material glowMaterial = loadedSign.m_textWidget.fontSharedMaterial!;
        Assert(!ReferenceEquals(originalMaterial, glowMaterial),
            "the runtime-shaped Sign Glow stub must replace the widget material");
        loadedSign.GetComponent<SignGlowController>()!.RestoreAndRemove();
        Assert(glowMaterial.Destroyed && !originalMaterial.Destroyed,
            "Sign Glow cleanup must destroy only its cloned material");
        WorldLabelRuntime.Reset();
    }

    private static DamageText CreateDamageText(TMP_FontAsset font, Material material)
    {
        GameObject damageTextObject = new GameObject("DamageText");
        DamageText damageText = damageTextObject.AddComponent<DamageText>();
        GameObject template = new GameObject("WorldTextBase");
        TextMeshProUGUI templateText = template.AddComponent<TextMeshProUGUI>();
        templateText.font = font;
        templateText.fontSharedMaterial = material;
        templateText.StyleMarker = "native-high-contrast";
        damageText.m_worldTextBase = template;
        return damageText;
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

    private static void InvokeLateUpdate(PortalLabelController controller) =>
        typeof(PortalLabelController)
            .GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(controller, null);

    private static GameObject? GetLabelRoot(PortalLabelController controller) =>
        (GameObject?)typeof(PortalLabelController)
            .GetField("labelRoot", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller);

    private static TMP_Text? GetLabel(PortalLabelController controller) =>
        (TMP_Text?)typeof(PortalLabelController)
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
