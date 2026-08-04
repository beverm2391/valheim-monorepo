using BenheimQoL.InventoryFeature;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Shortcuts;

internal static class ShortcutOverlay
{
    private static readonly Section[] Sections =
    {
        new Section(
            "Inventory",
            new Color(1f, 0.82f, 0.28f, 1f),
            new[]
            {
                new Entry("Gold P", "Manual; toggle with P; persists when moved", PocketMarker.ManualColor),
                new Entry("Hover + P", "Toggle manual pocketing for this stack or item"),
                new Entry("Left Alt + click", "Toggle manual pocketing for this stack or item"),
                new Entry("Left Shift + P", $"Put matching items away within {QuickStack.Radius:0.#} m"),
                new Entry("Backspace/Delete", "Reset the split amount to 1"),
                new Entry("Enter", "Confirm a split; move it across an open container"),
            },
            "A gold P means manually pocketed. Stackables protect their item type; gear protects only the marked item. Equipped and hotbar items stay protected without a marker."),
        new Section(
            "Build & Repair",
            new Color(1f, 0.58f, 0.36f, 1f),
            new[]
            {
                new Entry("Shift + station click", "Repair all eligible gear"),
                new Entry("Left Shift + station input", "Fill its available input or fuel capacity"),
            },
            "Stations, cauldrons, and nearby objects have a longer interaction range."),
        new Section(
            "Farming",
            new Color(0.48f, 0.88f, 0.45f, 1f),
            new[]
            {
                new Entry("Left Shift + interact", $"Harvest matching targets within {Farming.FarmingSettings.HarvestRadius:0.#} m"),
                new Entry("Left Shift + plant", $"Plant a centered {Farming.FarmingSettings.GridWidth}x{Farming.FarmingSettings.GridLength} grid"),
            },
            "Normal resource, stamina, spacing, and cultivated-ground rules still apply."),
        new Section(
            "Travel",
            new Color(0.42f, 0.84f, 1f, 1f),
            new Entry[0],
            "Portal transitions finish sooner after the destination is ready."),
        new Section(
            "Combat & Skills",
            new Color(1f, 0.46f, 0.5f, 1f),
            new Entry[0],
            "Pickaxes skill improves mining damage, crits, and AOE after level 25. " +
            "Wood Cutting unlocks CLEAVE after level 25. " +
            "Perfect defenses show adrenaline gains, and the meter shows decay timing."),
        new Section(
            "Help",
            new Color(0.74f, 0.7f, 1f, 1f),
            new[]
            {
                new Entry("F7", "Save a diagnostic log to the Desktop"),
                new Entry("F8", "Show or hide this panel"),
            },
            "Send the exported Benheim log when reporting a problem."),
    };

    private static readonly string Title = $"Benheim v{Plugin.PluginVersion}";
    private static readonly Rect PreloadRect = new Rect(0f, 0f, 1000f, 100f);

    private static bool visible;
    private static bool preloaded;
    private static Vector2 scrollPosition;
    private static GUIStyle? titleStyle;
    private static GUIStyle? keyStyle;
    private static GUIStyle? bodyStyle;
    private static GUIStyle? noteStyle;
    private static GUIStyle? sectionStyle;
    private static GUIStyle? panelStyle;
    private static Texture2D? panelBackground;

    internal static void Update()
    {
        if (InputState.IsKeyDown(KeyCode.F8))
        {
            visible = !visible;
            Diagnostics.Event("Shortcuts", "panel_toggled", $"visible={Diagnostics.Bool(visible)}");
        }
    }

    internal static void Draw()
    {
        EnsureStyles();
        PreloadTextOnce();
        if (!visible)
        {
            return;
        }

        float width = Mathf.Max(320f, Mathf.Min(980f, Screen.width - 64f));
        float height = Mathf.Max(320f, Screen.height - 220f);
        Rect rect = new Rect(32f, 180f, width, height);
        GUILayout.BeginArea(rect, panelStyle);
        GUILayout.Label(Title, titleStyle);
        GUILayout.Space(12f);

        scrollPosition = GUILayout.BeginScrollView(
            scrollPosition,
            alwaysShowHorizontal: false,
            alwaysShowVertical: false);
        foreach (Section section in Sections)
        {
            sectionStyle!.normal.textColor = section.Accent;
            keyStyle!.normal.textColor = section.Accent;
            GUILayout.Label(section.Name, sectionStyle);
            foreach (Entry entry in section.Entries)
            {
                keyStyle!.normal.textColor = entry.Accent ?? section.Accent;
                GUILayout.BeginHorizontal();
                GUILayout.Label(entry.Key, keyStyle, GUILayout.Width(360f));
                GUILayout.Label(entry.Action, bodyStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(3f);
            }

            GUILayout.Label(section.Note, noteStyle);
            GUILayout.Space(10f);
        }
        GUILayout.EndScrollView();

        GUILayout.Label("F8  Close", noteStyle);
        GUILayout.EndArea();
    }

    private static void PreloadTextOnce()
    {
        if (preloaded || Event.current.type != EventType.Repaint)
        {
            return;
        }

        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.001f);
        GUI.Label(PreloadRect, Title, titleStyle);
        foreach (Section section in Sections)
        {
            GUI.Label(PreloadRect, section.Name, sectionStyle);
            foreach (Entry entry in section.Entries)
            {
                GUI.Label(PreloadRect, entry.Key, keyStyle);
                GUI.Label(PreloadRect, entry.Action, bodyStyle);
            }

            GUI.Label(PreloadRect, section.Note, noteStyle);
        }

        GUI.color = previousColor;
        preloaded = true;
        Diagnostics.Event("Shortcuts", "panel_preloaded", $"sections={Sections.Length}");
    }

    private static void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        panelBackground = new Texture2D(1, 1);
        panelBackground.SetPixel(0, 0, new Color(0.03f, 0.04f, 0.05f, 0.94f));
        panelBackground.Apply();

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(28, 28, 24, 24),
            normal = { background = panelBackground },
        };

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 42,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
        };

        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold,
        };

        keyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
        };

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            wordWrap = true,
            normal = { textColor = Color.white },
        };

        noteStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 23,
            wordWrap = true,
            normal = { textColor = new Color(0.82f, 0.84f, 0.86f, 1f) },
        };
    }

    private readonly struct Entry
    {
        internal Entry(string key, string action, Color? accent = null)
        {
            Key = key;
            Action = action;
            Accent = accent;
        }

        internal string Key { get; }
        internal string Action { get; }
        internal Color? Accent { get; }
    }

    private sealed class Section
    {
        internal Section(string name, Color accent, Entry[] entries, string note)
        {
            Name = name;
            Accent = accent;
            Entries = entries;
            Note = note;
        }

        internal string Name { get; }
        internal Color Accent { get; }
        internal Entry[] Entries { get; }
        internal string Note { get; }
    }
}
