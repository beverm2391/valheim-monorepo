using BenheimQoL.InventoryFeature;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Shortcuts;

internal static class ShortcutOverlay
{
    private static readonly (string Key, string Action)[] Shortcuts =
    {
        ("F8", "Show or hide this BenheimQoL shortcuts panel"),
        ("Inventory: hover + P", "Pocket or unpocket this item type"),
        ("Inventory: Left Alt + click", "Pocket or unpocket this item type"),
        ("Left Shift + P", $"Put matching non-pocketed items into accessible chests within {QuickStack.Radius:0.#} m"),
        ("Split stack: Backspace/Delete", "Clear split amount back to 1"),
        ("Split stack: Enter", "Confirm split; with a container open, move it across"),
        ("Station repair: Left Shift + click", "Repair all eligible gear"),
        ("Hammer repair: Left Shift + click", "Repair nearby damaged buildings and structures"),
        ("Portal tag edit: Tab", "Cycle known portal tag matches"),
    };

    private static readonly string[] PassiveFeatures =
    {
        "Longer station/interact range",
        "Faster portal transition after the target area is ready",
        "Pickaxes skill increases mining damage, crits, and AOE after level 25",
        "Perfect parries/dodges show gains; the adrenaline meter shows decay timing",
    };

    private static bool visible;
    private static GUIStyle? titleStyle;
    private static GUIStyle? keyStyle;
    private static GUIStyle? bodyStyle;
    private static GUIStyle? sectionStyle;
    private static GUIStyle? panelStyle;
    private static Texture2D? panelBackground;

    internal static void Update()
    {
        if (TextInput.IsVisible() || Console.IsVisible())
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            visible = !visible;
            Diagnostics.Event("Shortcuts", "panel_toggled", $"visible={Diagnostics.Bool(visible)}");
        }
    }

    internal static void Draw()
    {
        if (!visible)
        {
            return;
        }

        EnsureStyles();

        float width = Mathf.Min(940f, Screen.width - 80f);
        float height = Screen.height - 220f;
        Rect rect = new Rect(32f, 180f, width, height);
        GUILayout.BeginArea(rect, panelStyle);
        GUILayout.Label($"BenheimQoL v{Plugin.PluginVersion} Shortcuts", titleStyle);
        GUILayout.Space(14f);

        GUILayout.Label("Keys", sectionStyle);
        foreach ((string key, string action) in Shortcuts)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(key, keyStyle, GUILayout.Width(380f));
            GUILayout.Label(action, bodyStyle);
            GUILayout.EndHorizontal();
            GUILayout.Space(5f);
        }

        GUILayout.Space(14f);
        GUILayout.Label("Passive", sectionStyle);
        foreach (string feature in PassiveFeatures)
        {
            GUILayout.Label("- " + feature, bodyStyle);
        }

        GUILayout.Space(12f);
        GUILayout.Label("Press F8 to hide.", bodyStyle);
        GUILayout.EndArea();
    }

    private static void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        panelBackground = new Texture2D(1, 1);
        panelBackground.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.05f, 0.92f));
        panelBackground.Apply();

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(28, 28, 24, 24),
            normal = { background = panelBackground },
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 44,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.86f, 0.25f, 1f) },
        };

        keyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.75f, 0.9f, 1f, 1f) },
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            wordWrap = true,
            normal = { textColor = Color.white },
        };
    }
}
