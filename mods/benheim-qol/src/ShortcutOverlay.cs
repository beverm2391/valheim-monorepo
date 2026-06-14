using UnityEngine;

namespace BenheimQoL;

internal static class ShortcutOverlay
{
    private static readonly (string Key, string Action)[] Shortcuts =
    {
        ("F8", "Show or hide this BenheimQoL shortcuts panel"),
        ("Inventory: hover + P", "Pocket or unpocket this item type"),
        ("Inventory: Left Alt + click", "Pocket or unpocket this item type"),
        ("Inventory: Left Alt + P", "Quick stack matching non-pocketed items"),
        ("Split stack: Backspace/Delete", "Clear split amount back to 1"),
        ("Split stack: Enter", "Confirm split; with a container open, move it across"),
        ("Station repair: Left Shift + click", "Repair all eligible gear"),
        ("Hammer repair: Left Shift + click", "Repair nearby damaged building pieces"),
        ("Portal tag edit: Tab", "Cycle known portal tag matches"),
    };

    private static readonly string[] PassiveFeatures =
    {
        "Longer station/interact range",
        "Faster portal transition after the target area is ready",
        "Pickaxes skill increases mining damage, crits, and high-skill AOE",
    };

    private static bool visible;
    private static GUIStyle? titleStyle;
    private static GUIStyle? keyStyle;
    private static GUIStyle? bodyStyle;
    private static GUIStyle? sectionStyle;

    internal static void Update()
    {
        if (TextInput.IsVisible() || Console.IsVisible())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            visible = !visible;
        }
    }

    internal static void Draw()
    {
        if (!visible)
        {
            return;
        }

        EnsureStyles();

        float width = Mathf.Min(560f, Screen.width - 40f);
        Rect rect = new Rect(20f, 20f, width, 430f);
        GUILayout.BeginArea(rect, GUI.skin.window);
        GUILayout.Label($"BenheimQoL v{Plugin.PluginVersion} Shortcuts", titleStyle);
        GUILayout.Space(8f);

        GUILayout.Label("Keys", sectionStyle);
        foreach ((string key, string action) in Shortcuts)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, keyStyle, GUILayout.Width(190f));
            GUILayout.Label(action, bodyStyle);
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10f);
        GUILayout.Label("Passive", sectionStyle);
        foreach (string feature in PassiveFeatures)
        {
            GUILayout.Label("- " + feature, bodyStyle);
        }

        GUILayout.Space(8f);
        GUILayout.Label("Press F8 to hide.", bodyStyle);
        GUILayout.EndArea();
    }

    private static void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.86f, 0.25f, 1f) },
        };

        keyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.75f, 0.9f, 1f, 1f) },
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
    }
}
