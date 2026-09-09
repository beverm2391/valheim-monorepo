using BenheimQoL.CombatFeedback;
using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private static Toggle? fxMasterToggle;
    private static Toggle? bowFocusToggle;
    private static Toggle? combatShakeToggle;
    private static Toggle? dangerArrivalToggle;
    private static Toggle? shareDiagnosticsToggle;

    private static void BuildFxConfig(RectTransform parent, NativeTemplates templates)
    {
        AddSectionHeading(parent, "Benheim FX", ConfigAccent, templates.Text);

        TMP_Text explanation = CreateText(
            "BenheimFxExplanation",
            parent,
            templates.Text,
            layoutElement: true);
        explanation.fontSize = 18f;
        explanation.color = Color.white;
        explanation.text =
            "The master controls Bow Focus, Combat Shake, and Danger Arrival FX. " +
            "Map labels, gameplay (including Snipe's automatic scope), and native Valheim effects stay on.";

        fxMasterToggle = AddConfigToggle(
            parent,
            templates,
            "Benheim FX",
            BenheimFxSettings.MasterEnabled,
            enabled =>
            {
                BenheimFxSettings.SetMaster(enabled);
                RefreshFxConfigInteractivity();
                LogFxSetting("master", enabled);
            });
        bowFocusToggle = AddConfigToggle(
            parent,
            templates,
            "Bow Focus",
            BenheimFxSettings.BowFocusPreference,
            enabled =>
            {
                BenheimFxSettings.SetBowFocus(enabled);
                LogFxSetting("bow_focus", enabled);
            });
        combatShakeToggle = AddConfigToggle(
            parent,
            templates,
            "Combat Shake",
            BenheimFxSettings.CombatShakePreference,
            enabled =>
            {
                BenheimFxSettings.SetCombatShake(enabled);
                LogFxSetting("combat_shake", enabled);
            });
        dangerArrivalToggle = AddConfigToggle(
            parent,
            templates,
            "Danger Arrival FX",
            BenheimFxSettings.DangerArrivalPreference,
            enabled =>
            {
                BenheimFxSettings.SetDangerArrival(enabled);
                LogFxSetting("danger_arrival", enabled);
            });

        TMP_Text families = CreateText(
            "BenheimFxFamilies",
            parent,
            templates.Text,
            layoutElement: true);
        families.fontSize = 17f;
        families.color = new Color(0.78f, 0.8f, 0.82f, 1f);
        families.text =
            "Bow Focus narrows FOV during ordinary bow draw. Snipe's automatic scope stays active with Bow Focus or Benheim FX off. Combat Shake covers headshots, " +
            "Cleave, mining AOE, and Perfect Impact. Danger Arrival FX covers its " +
            "banner, stinger, and brief edge vignette.";

        AddSectionHeading(parent, "Diagnostics", ConfigAccent, templates.Text);
        TMP_Text diagnosticsExplanation = CreateText(
            "DiagnosticsExplanation",
            parent,
            templates.Text,
            layoutElement: true);
        diagnosticsExplanation.fontSize = 18f;
        diagnosticsExplanation.color = Color.white;
        diagnosticsExplanation.text = RemoteDiagnostics.IsConfigured
            ? "Share Diagnostics sends typed gameplay events, your character name, and a connection ID " +
                "from this private test build. Sharing starts on. Turning it off preserves your choice. " +
                "It never sends chat or full logs. Local diagnostics always stay on."
            : "Remote diagnostics are not configured in this build. Local diagnostics always stay on.";

        shareDiagnosticsToggle = AddConfigToggle(
            parent,
            templates,
            "Share Diagnostics",
            DiagnosticsSharingSettings.ShareDiagnostics,
            enabled =>
            {
                DiagnosticsSharingSettings.SetShareDiagnostics(enabled);
                Diagnostics.Event(
                    "Diagnostics",
                    "sharing_setting_changed",
                    $"enabled={Diagnostics.Bool(enabled)} configured={Diagnostics.Bool(RemoteDiagnostics.IsConfigured)}");
            });

        RefreshFxConfigInteractivity();
        Diagnostics.Event(
            "Shortcuts",
            "fx_config_built",
            "toggles=4 labels=4 label_layout=explicit_native_text");
    }

    private static Toggle AddConfigToggle(
        RectTransform parent,
        NativeTemplates templates,
        string label,
        bool value,
        UnityAction<bool> changed)
    {
        const float rowHeight = 38f;

        RectTransform row = CreateRectObject(label.Replace(" ", "") + "Row", parent);
        HorizontalLayoutGroup rowLayout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        LayoutElement rowSize = row.gameObject.AddComponent<LayoutElement>();
        rowSize.minHeight = rowHeight;
        rowSize.preferredHeight = rowHeight;

        // AccessibilitySettings exposes the native checkbox control, not a label
        // contract. Compose the label explicitly from Benheim's established native
        // text donor so every setting has visible text regardless of prefab hierarchy.
        Toggle toggle = Object.Instantiate(templates.Checkbox, row, worldPositionStays: false);
        toggle.name = label.Replace(" ", "") + "Toggle";
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.SetIsOnWithoutNotify(value);
        toggle.onValueChanged.AddListener(changed);

        LayoutElement toggleSize = toggle.GetComponent<LayoutElement>()
            ?? toggle.gameObject.AddComponent<LayoutElement>();
        toggleSize.ignoreLayout = false;
        toggleSize.minWidth = rowHeight;
        toggleSize.preferredWidth = rowHeight;
        toggleSize.flexibleWidth = 0f;
        toggleSize.minHeight = rowHeight;
        toggleSize.preferredHeight = rowHeight;

        TMP_Text toggleLabel = CreateText(
            "FxToggleLabel",
            row,
            templates.Text,
            layoutElement: true);
        toggleLabel.text = label;
        toggleLabel.alignment = TextAlignmentOptions.MidlineLeft;
        toggleLabel.textWrappingMode = TextWrappingModes.NoWrap;
        LayoutElement labelSize = toggleLabel.GetComponent<LayoutElement>();
        labelSize.minHeight = rowHeight;
        labelSize.preferredHeight = rowHeight;
        labelSize.flexibleWidth = 1f;
        return toggle;
    }

    private static void RefreshFxConfigInteractivity()
    {
        bool masterEnabled = fxMasterToggle?.isOn ?? BenheimFxSettings.MasterEnabled;
        if (bowFocusToggle != null)
        {
            bowFocusToggle.interactable = masterEnabled;
        }
        if (combatShakeToggle != null)
        {
            combatShakeToggle.interactable = masterEnabled;
        }
        if (dangerArrivalToggle != null)
        {
            dangerArrivalToggle.interactable = masterEnabled;
        }
    }

    private static void LogFxSetting(string setting, bool enabled)
    {
        Diagnostics.Event(
            "CombatFeedback",
            "fx_setting_changed",
            $"setting={setting} enabled={Diagnostics.Bool(enabled)} " +
            $"master_enabled={Diagnostics.Bool(BenheimFxSettings.MasterEnabled)}");
    }

    private static void ResetFxConfigState()
    {
        fxMasterToggle = null;
        bowFocusToggle = null;
        combatShakeToggle = null;
        dangerArrivalToggle = null;
        shareDiagnosticsToggle = null;
    }
}
