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
            "Map labels, gameplay, and native Valheim effects stay on.";

        fxMasterToggle = AddFxToggle(
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
        bowFocusToggle = AddFxToggle(
            parent,
            templates,
            "Bow Focus",
            BenheimFxSettings.BowFocusPreference,
            enabled =>
            {
                BenheimFxSettings.SetBowFocus(enabled);
                LogFxSetting("bow_focus", enabled);
            });
        combatShakeToggle = AddFxToggle(
            parent,
            templates,
            "Combat Shake",
            BenheimFxSettings.CombatShakePreference,
            enabled =>
            {
                BenheimFxSettings.SetCombatShake(enabled);
                LogFxSetting("combat_shake", enabled);
            });
        dangerArrivalToggle = AddFxToggle(
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
            "Bow Focus narrows FOV during bow draw. Combat Shake covers headshots, " +
            "Cleave, and mining AOE. Danger Arrival FX covers its banner, stinger, " +
            "and brief edge vignette.";

        RefreshFxConfigInteractivity();
    }

    private static Toggle AddFxToggle(
        RectTransform parent,
        NativeTemplates templates,
        string label,
        bool value,
        UnityAction<bool> changed)
    {
        Toggle toggle = Object.Instantiate(templates.Checkbox, parent, worldPositionStays: false);
        toggle.name = label.Replace(" ", "") + "Toggle";
        toggle.onValueChanged = new Toggle.ToggleEvent();
        toggle.SetIsOnWithoutNotify(value);
        toggle.onValueChanged.AddListener(changed);

        TMP_Text? toggleLabel = toggle.GetComponentInChildren<TMP_Text>(includeInactive: true);
        if (toggleLabel != null)
        {
            toggleLabel.text = label;
        }

        LayoutElement layout = toggle.GetComponent<LayoutElement>()
            ?? toggle.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = 38f;
        layout.preferredHeight = 38f;
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
    }
}
