using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using BenheimQoL.Infrastructure;
using BenheimQoL.WorldLabels;
using TMPro;
using UnityEngine;

internal static class Program
{
    private static void Main()
    {
        VerifyNativeDonorBoundary();
        VerifyPortalBoardLifecycle();
        VerifyPortalDiagnosticEvidence();
        Console.WriteLine("World Label runtime-shaped sign-board contract checks passed");
    }

    private static void VerifyNativeDonorBoundary()
    {
        WorldLabelRuntime.Reset();
        Plugin.Log.Clear();
        Diagnostics.Events.Clear();
        ZNetScene.instance = new ZNetScene();

        Sign decorativeSign = CreateNativeSign("sign_darkwood", "$piece_sign_darkwood");
        ZNetScene.instance.m_prefabs.Add(decorativeSign.gameObject);
        Assert(!WorldLabelRuntime.TryGetNativeWoodenSign(out _),
            "a different Sign prefab must not become the wooden-board donor");

        Sign woodenSign = CreateNativeSign("sign", "$piece_sign");
        ZNetScene.instance.m_prefabs.Add(woodenSign.gameObject);
        Assert(WorldLabelRuntime.TryGetNativeWoodenSign(out Sign resolved) &&
            ReferenceEquals(resolved, woodenSign),
            "the runtime must resolve the installed native sign prefab by its Piece contract");
        Assert(Diagnostics.Events.Count(record => record.Name == "portal_sign_donor_resolved") == 1,
            "native donor resolution must emit one typed record");
    }

    private static void VerifyPortalBoardLifecycle()
    {
        WorldLabelRuntime.Reset();
        Plugin.Log.Clear();
        Diagnostics.Events.Clear();
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
        Assert(GetRoot(controller) == null && Diagnostics.Events.Count(record =>
                record.Name == "portal_label" && Json(record).GetProperty("state").GetString() ==
                    "native_sign_not_loaded") == 1,
            "a non-empty tag must emit its missing native donor boundary once");

        Sign donor = CreateNativeSign("sign(Clone)", "$piece_sign");
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
        VerifyFittingConfiguration(labels, donor.m_textWidget);
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
        foreach (string tag in new[] { "TRAVEL11", "TRAVEL12", "WWWWWWWWWW", "I", "<b>exact</b>" })
        {
            portal.Tag = tag;
            InvokeRefresh(controller);
            Assert(ReferenceEquals(originalRoot, GetRoot(controller)) &&
                labels.All(label => label.text == tag),
                "renaming must update both existing faces exactly without duplication");
            VerifyFittingConfiguration(labels, donor.m_textWidget);
        }

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

    private static void VerifyPortalDiagnosticEvidence()
    {
        WorldLabelRuntime.Reset();
        Diagnostics.Events.Clear();
        ZNetScene.instance = new ZNetScene();
        Sign donor = CreateNativeSign("sign", "$piece_sign");
        Material material = donor.m_textWidget.fontSharedMaterial!;
        donor.m_textWidget.fontSharedMaterial = null;
        ZNetScene.instance.m_prefabs.Add(donor.gameObject);
        TeleportWorld portal = CreatePortal("portal_wood", "TRAVEL11");
        WorldLabelRuntime.Attach(portal);
        PortalLabelController controller = portal.GetComponent<PortalLabelController>()!;
        InvokeRefresh(controller);
        Assert(LastPortalRecord().GetProperty("state").GetString() == "native_sign_missing_material",
            "a present but incomplete donor must be distinguishable from a missing donor");
        donor.m_textWidget.fontSharedMaterial = material;
        InvokeRefresh(controller);
        JsonElement creation = LastPortalRecord();
        Assert(creation.GetProperty("change").GetString() == "created" &&
            creation.GetProperty("tag").GetString() == "TRAVEL11" &&
            creation.GetProperty("front_outcome").GetString() == "no_visible_glyphs" &&
            creation.GetProperty("back_outcome").GetString() == "no_visible_glyphs",
            "creation must not claim fitting when TMP has supplied no visible geometry");
        GameObject root = GetRoot(controller)!;
        TextMeshProUGUI[] faces = root.GetComponentsInChildren<TextMeshProUGUI>(true);

        // Feed observations at the TMP boundary, not a fabricated font engine.
        // This verifies emitted classification and numbers; live TMP rendering
        // remains the source of the measurements in the game.
        foreach (TextMeshProUGUI face in faces)
        {
            face.textInfo.characterCount = 8;
            face.textInfo.lineCount = 1;
            face.textInfo.characterInfo = Enumerable.Range(0, 8)
                .Select(_ => new TMP_CharacterInfo { isVisible = true }).ToArray();
            face.textBounds = new Bounds(new Vector3(-5f, -2f, 0f), new Vector3(5f, 2f, 0f));
        }
        faces[1].textBounds = new Bounds(new Vector3(-5f, -5f, 0f), new Vector3(5f, 2f, 0f));
        InvokeRefresh(controller);
        JsonElement layout = LastPortalRecord();
        Assert(layout.GetProperty("change").GetString() == "layout_changed" &&
            layout.GetProperty("front_outcome").GetString() == "fit" &&
            layout.GetProperty("back_outcome").GetString() == "overflow" &&
            layout.GetProperty("back_text_bottom").GetSingle() == -5f &&
            layout.GetProperty("back_fit_bottom").GetSingle() > -5f &&
            layout.GetProperty("front_characters").GetInt32() == 8,
            "each face must emit its observed geometry and independent containment result");
        int records = Diagnostics.Events.Count;
        int meshUpdates = faces.Sum(face => face.MeshUpdateCalls);
        InvokeRefresh(controller);
        InvokeRefresh(controller);
        Assert(Diagnostics.Events.Count == records && faces.Sum(face => face.MeshUpdateCalls) == meshUpdates,
            "unchanged refreshes must neither repeat evidence nor force TMP regeneration");

        faces[0].havePropertiesChanged = true;
        InvokeRefresh(controller);
        Assert(LastPortalRecord().GetProperty("front_outcome").GetString() == "layout_pending",
            "dirty TMP properties must not turn stale geometry into a successful fit claim");
        faces[0].havePropertiesChanged = false;

        faces[1].textBounds = faces[0].textBounds;
        faces[1].isTextOverflowing = true;
        InvokeRefresh(controller);
        Assert(LastPortalRecord().GetProperty("back_outcome").GetString() == "overflow" &&
            LastPortalRecord().GetProperty("back_tmp_overflow").GetBoolean(),
            "TMP overflow must remain visible even when the supplied bounds fit");
        faces[1].isTextOverflowing = false;
        faces[0].fontSharedMaterial = null;
        InvokeRefresh(controller);
        Assert(LastPortalRecord().GetProperty("front_outcome").GetString() == "missing_material" &&
            LastPortalRecord().GetProperty("back_outcome").GetString() == "fit",
            "a missing component on one face must not suppress the other face's result");
        faces[0].fontSharedMaterial = material;
        faces[0].ThrowOnMeshUpdate = true;
        portal.Tag = "TRAVEL12";
        InvokeRefresh(controller);
        Assert(LastPortalRecord().GetProperty("change").GetString() == "tag_changed" &&
            LastPortalRecord().GetProperty("front_outcome").GetString() ==
                "measurement_failed:InvalidOperationException" && faces.All(face => face.text == "TRAVEL12"),
            "measurement failure must be typed without preventing the actual rename");
        faces[0].ThrowOnMeshUpdate = false;
        Diagnostics.ThrowOnEmit = true;
        portal.Tag = "HOME";
        InvokeRefresh(controller);
        Diagnostics.ThrowOnEmit = false;
        Assert(faces.All(face => face.text == "HOME") && ReferenceEquals(GetRoot(controller), root),
            "an unavailable diagnostic sink must leave the existing board and rename behavior intact");
        UnityEngine.Object.Destroy(faces[0]);
        InvokeRefresh(controller);
        Assert(LastPortalRecord().GetProperty("front_outcome").GetString() == "missing_text_widget",
            "a destroyed face must be reported rather than throwing during observation");
        portal.Tag = string.Empty;
        InvokeRefresh(controller);
        Assert(LastPortalRecord().GetProperty("state").GetString() == "empty_tag" && root.Destroyed,
            "clearing the tag must report the hidden state and still remove the visual");
        WorldLabelRuntime.Reset();
    }

    private static JsonElement LastPortalRecord() =>
        Json(Diagnostics.Events.Last(record => record.Name == "portal_label"));

    private static JsonElement Json(DiagnosticEvent record)
    {
        using JsonDocument document = JsonDocument.Parse(record.ToJsonLine());
        return document.RootElement.Clone();
    }

    private static void VerifyFittingConfiguration(TextMeshProUGUI[] labels, TextMeshProUGUI donor)
    {
        // These stubs prove the native TMP configuration survives creation and
        // rename. They deliberately do not simulate TMP's glyph layout.
        foreach (TextMeshProUGUI label in labels)
        {
            Assert(label.enableAutoSizing && label.fontSizeMin == donor.fontSizeMin &&
                label.fontSizeMax == donor.fontSizeMax && label.fontSizeMin > 0f &&
                label.fontSizeMin < label.fontSizeMax,
                "each face must retain the native font's shrink range");
            Assert(label.textWrappingMode == TextWrappingModes.NoWrap &&
                label.overflowMode == TextOverflowModes.Overflow,
                "TMP must fit the complete tag on one line without truncation");
            Rect rect = label.rectTransform.rect;
            Vector4 inset = label.margin;
            Assert(inset.x > 0f && inset.y > 0f && inset.z > 0f && inset.w > 0f &&
                inset.x + inset.z < rect.width && inset.y + inset.w < rect.height,
                "the fit rectangle must leave space on every side inside the native text area");
            Assert(rect.width == donor.rectTransform.rect.width &&
                rect.height == donor.rectTransform.rect.height &&
                label.transform.localScale.x == donor.transform.localScale.x &&
                label.transform.localScale.y == donor.transform.localScale.y,
                "fitting must preserve the accepted native text-face geometry");
        }
    }

    private static Sign CreateNativeSign(string name, string pieceName)
    {
        GameObject root = new(name);
        root.AddComponent<Piece>().m_name = pieceName;
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
        textObject.transform.localScale = new Vector3(0.05f, 0.05f, 1f);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = new TMP_FontAsset();
        text.fontSharedMaterial = new Material { name = "Native Sign Text" };
        // Installed wooden sign asset: native TMP autosizing was the setting
        // the portal-face copy omitted when the wrapping regression occurred.
        text.fontSize = 7.75f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 1f;
        text.fontSizeMax = 8f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.color = Color.white;
        ((RectTransform)textObject.transform).sizeDelta = new Vector2(18.2888f, 8.5506f);
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
