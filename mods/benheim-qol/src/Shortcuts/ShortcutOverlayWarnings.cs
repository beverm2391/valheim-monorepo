using System;
using System.Collections.Generic;
using System.Reflection;
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
            row.text = $"<b>{warning.Key} — {warning.Action}</b> conflicts with native {warning.NativeAction}";
            row.fontSize = 16f;
            row.color = new Color(1f, 0.88f, 0.62f, 1f);
        }
    }

    private static List<ShortcutWarning> FindShortcutWarnings()
    {
        List<ShortcutWarning> warnings = new();
        if (ZInput.instance == null
            || ButtonsField?.GetValue(ZInput.instance) is not Dictionary<string, ZInput.ButtonDef> buttons)
        {
            return warnings;
        }

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

        return warnings;
    }

    private readonly struct ShortcutWarning
    {
        internal ShortcutWarning(string key, string action, string nativeAction)
        {
            Key = key;
            Action = action;
            NativeAction = nativeAction;
        }

        internal string Key { get; }
        internal string Action { get; }
        internal string NativeAction { get; }
    }
}
