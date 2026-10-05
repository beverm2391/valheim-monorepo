using BenheimQoL.SpawnProtection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private static Toggle? spawnProtectionToggle;

    private static void BuildSpawnProtectionConfig(RectTransform parent, NativeTemplates templates)
    {
        AddSectionHeading(parent, "Building", ConfigAccent, templates.Text);
        TMP_Text explanation = CreateText("SpawnProtectionExplanation", parent, templates.Text, layoutElement: true);
        explanation.fontSize = 18f;
        explanation.color = Color.white;
        explanation.text = "F8 shows nearby base pieces' combined spawn protection boundary and grey [no_spawn] beneath the minimap when your position is protected. The rings are a horizontal preview, not exact coverage on hills or around elevated pieces. Starts off each game session.";
        spawnProtectionToggle = AddConfigToggle(parent, templates, "Spawn protection overlay", SpawnProtectionOverlay.Enabled,
            enabled =>
            {
                SpawnProtectionOverlay.SetEnabled(enabled, "config");
                RefreshSpawnProtectionConfig();
            });
    }

    private static void RefreshSpawnProtectionConfig()
    {
        // Silent synchronization reflects F8, cleanup, and rejected enables
        // without re-entering the checkbox callback.
        if (spawnProtectionToggle != null)
            spawnProtectionToggle.SetIsOnWithoutNotify(SpawnProtectionOverlay.Enabled);
    }
}
