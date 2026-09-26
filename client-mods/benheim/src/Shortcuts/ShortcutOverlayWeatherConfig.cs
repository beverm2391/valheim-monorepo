using BenheimQoL.Infrastructure;
using BenheimQoL.WeatherVisibility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private static Toggle? blizzardVisibilityToggle;

    private static void BuildWeatherVisibilityConfig(
        RectTransform parent,
        NativeTemplates templates)
    {
        AddSectionHeading(parent, "Visibility", ConfigAccent, templates.Text);

        TMP_Text explanation = CreateText(
            "WeatherVisibilityExplanation",
            parent,
            templates.Text,
            layoutElement: true);
        explanation.fontSize = 18f;
        explanation.color = Color.white;
        explanation.text =
            "Blizzard Visibility keeps Mountain snowstorms stormy while making terrain and Frost Cave entrances readable. Weather timing, wind, audio, cold, and freezing stay native.";

        blizzardVisibilityToggle = AddConfigToggle(
            parent,
            templates,
            "Blizzard Visibility",
            BlizzardVisibilitySettings.Enabled,
            BlizzardVisibilitySettings.SetEnabled);

        Diagnostics.Emit(DiagnosticEvent.Create("Shortcuts", "visibility_config_built")
            .Boolean("blizzard_visibility_enabled", BlizzardVisibilitySettings.Enabled));
    }

    private static void ResetWeatherVisibilityConfigState()
    {
        blizzardVisibilityToggle = null;
    }
}
