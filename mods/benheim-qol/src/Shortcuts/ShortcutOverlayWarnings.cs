using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private static readonly FieldInfo? ButtonsField =
        AccessTools.Field(typeof(ZInput), "m_buttons");

    private static GameObject? controlsWarnings;

    private static void BuildControlsWarnings(RectTransform parent)
    {
        controlsWarnings = CreateRectObject("Warnings", parent).gameObject;
        VerticalLayoutGroup layout = controlsWarnings.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 8, 8);
        layout.spacing = 3f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        controlsWarnings.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        controlsWarnings.AddComponent<LayoutElement>();

        Image background = controlsWarnings.AddComponent<Image>();
        background.color = new Color(0.38f, 0.24f, 0.05f, 0.8f);
        background.raycastTarget = false;
    }

    private static void RefreshControlsWarnings(NativeTemplates templates)
    {
        if (controlsWarnings == null)
        {
            return;
        }

        foreach (Transform child in controlsWarnings.transform)
        {
            UnityEngine.Object.Destroy(child.gameObject);
        }

        List<ShortcutWarning> warnings = FindShortcutWarnings();
        controlsWarnings.SetActive(warnings.Count > 0);
        if (warnings.Count == 0)
        {
            return;
        }

        RectTransform warningsRect = (RectTransform)controlsWarnings.transform;
        TMP_Text heading = CreateText("WarningHeading", warningsRect, templates.Text, layoutElement: true);
        heading.text = "Warnings";
        heading.fontSize = 20f;
        heading.fontStyle = FontStyles.Bold;
        heading.color = new Color(1f, 0.76f, 0.28f, 1f);

        foreach (ShortcutWarning warning in warnings)
        {
            TMP_Text row = CreateText("Warning", warningsRect, templates.Text, layoutElement: true);
            // Collision rows retain the existing "conflicts with native {warning.NativeAction}"
            // wording; health rows use the same surface without inventing a second panel.
            row.text = warning.IsHealth
                ? $"<b>{EscapeMarkup(warning.Key)} — {EscapeMarkup(warning.Action)}</b>"
                : $"<b>{EscapeMarkup(warning.Key)} — {EscapeMarkup(warning.Action)}</b> conflicts with native {EscapeMarkup(warning.NativeAction)}";
            row.fontSize = 16f;
            row.color = warning.IsCoreFailure
                ? new Color(1f, 0.52f, 0.52f, 1f)
                : new Color(1f, 0.88f, 0.62f, 1f);
        }
    }

    private static List<ShortcutWarning> FindShortcutWarnings()
    {
        List<ShortcutWarning> warnings = new();
        AddHealthWarnings(warnings);

        // ZInput is constructed after the Benheim plugin. This is a normal
        // startup state, not an inspection failure, so retry without showing a
        // false warning until the native binding map exists.
        if (ZInput.instance == null)
        {
            return warnings;
        }

        if (ButtonsField == null)
        {
            HealthReporting.ReportKeybindInspectionFailure("m_buttons field was not found");
            AddHealthWarnings(warnings);
            return warnings;
        }

        object? buttonMap;
        try
        {
            buttonMap = ButtonsField.GetValue(ZInput.instance);
        }
        catch (Exception ex)
        {
            HealthReporting.ReportKeybindInspectionFailure(ex.Message);
            AddHealthWarnings(warnings);
            return warnings;
        }

        if (buttonMap == null && Player.m_localPlayer == null)
        {
            return warnings;
        }

        if (buttonMap is not Dictionary<string, ZInput.ButtonDef> buttons)
        {
            HealthReporting.ReportKeybindInspectionFailure("m_buttons was not a native binding map");
            AddHealthWarnings(warnings);
            return warnings;
        }

        if (buttons.Count == 0)
        {
            if (Player.m_localPlayer == null)
            {
                return warnings;
            }

            HealthReporting.ReportKeybindInspectionFailure("native binding map was empty");
            AddHealthWarnings(warnings);
            return warnings;
        }

        try
        {
            foreach (NativeBinding binding in NativeBindings)
            {
                foreach (KeyValuePair<string, ZInput.ButtonDef> native in buttons)
                {
                    if (string.Equals(native.Key, binding.IgnoredNativeAction, StringComparison.Ordinal)
                        || !string.Equals(native.Value.GetActionPath(), binding.Path, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    warnings.Add(new ShortcutWarning(binding.Key, binding.Action, native.Key));
                }
            }
        }
        catch (Exception ex)
        {
            HealthReporting.ReportKeybindInspectionFailure(ex.Message);
            AddHealthWarnings(warnings);
        }

        return warnings;
    }

    private static void AddHealthWarnings(List<ShortcutWarning> warnings)
    {
        warnings.Clear();
        if (HealthReporting.CoreFailureDetail is string coreFailure)
        {
            warnings.Add(new ShortcutWarning(
                HealthReporting.CoreFailureOwner,
                $"{HealthReporting.CoreFailureMessage} {coreFailure}",
                isCoreFailure: true));
        }
        if (HealthReporting.KeybindInspectionDetail is string keybindFailure)
        {
            warnings.Add(new ShortcutWarning(
                HealthReporting.KeybindInspectionOwner,
                keybindFailure,
                isCoreFailure: false));
        }
    }

    private static string EscapeMarkup(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    private readonly struct ShortcutWarning
    {
        internal ShortcutWarning(string key, string action, string nativeAction)
        {
            Key = key;
            Action = action;
            NativeAction = nativeAction;
            IsHealth = false;
            IsCoreFailure = false;
        }

        internal ShortcutWarning(string key, string action, bool isCoreFailure)
        {
            Key = key;
            Action = action;
            NativeAction = string.Empty;
            IsHealth = true;
            IsCoreFailure = isCoreFailure;
        }

        internal string Key { get; }
        internal string Action { get; }
        internal string NativeAction { get; }
        internal bool IsHealth { get; }
        internal bool IsCoreFailure { get; }
    }
}
