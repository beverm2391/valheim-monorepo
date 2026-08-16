using BenheimQoL.Archery;
using BenheimQoL.Farming;
using BenheimQoL.InventoryFeature;
using BenheimQoL.Repair;
using UnityEngine;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private static readonly Color ControlsAccent = new(1f, 0.78f, 0.25f, 1f);
    private static readonly Color FeaturesAccent = new(0.48f, 0.88f, 0.58f, 1f);
    private static readonly Color TravelAccent = new(0.48f, 0.82f, 1f, 1f);
    private static readonly Color ConfigAccent = new(0.74f, 0.7f, 1f, 1f);

    private static readonly Section[] ControlSections =
    {
        new(
            "Inventory",
            ControlsAccent,
            new[]
            {
                new Entry("P", "Pocket the hovered stack or item"),
                new Entry("Left Alt + click", "Pocket the clicked stack or item"),
                new Entry("Left Shift + P", $"Put matching items away within {QuickStack.Radius:0.#} m"),
                new Entry("R", "Swap hotbar loadout (replaces Hide weapons)"),
                new Entry("Backspace / Delete", "Reset the split amount to 1"),
                new Entry("Enter", "Confirm a split and move it across an open container"),
            },
            "A gold P marks manual protection. Stackables protect every stack of that item type; non-stackable gear protects only the marked item. Place stacks, Hold to stack, and Put Away keep protected items with you. Equipped and hotbar items stay protected without a marker."),
        new(
            "Crafting & Repair",
            new Color(1f, 0.58f, 0.36f, 1f),
            new[]
            {
                new Entry("Left Shift + station click", "Repair all eligible gear"),
                new Entry("Left Shift + hammer repair", $"Repair eligible buildings and structures within {BuildingRepair.RepairRadius:0.#} m"),
                new Entry("Left Shift + station input", "Fill its available input or fuel capacity"),
            },
            "Stations, cauldrons, chests, and nearby objects have a longer interaction range."),
        new(
            "Farming",
            FeaturesAccent,
            new[]
            {
                new Entry("Left Shift + interact", $"Harvest matching targets within {FarmingSettings.HarvestRadius:0.#} m"),
                new Entry("Left Shift + plant", $"Plant a centered {FarmingSettings.GridWidth}x{FarmingSettings.GridLength} grid"),
            },
            "Normal resource, stamina, spacing, and cultivated-ground rules still apply."),
    };

    private static readonly Section[] FeatureSections =
    {
        new(
            "World & Travel",
            TravelAccent,
            new[]
            {
                new Entry("Extended reach", "Use interactable objects from up to 8 m; open containers stay available to 10 m"),
                new Entry("Portal travel", "Finish the transition sooner after the destination is ready"),
            },
            "These features reduce waiting and positioning friction without automating play."),
        new(
            "Production",
            new Color(1f, 0.58f, 0.36f, 1f),
            new[]
            {
                new Entry("Stone Oven", "Baking and done-to-burn timing are halved; fuel stays normal"),
            },
            "Faster baking preserves Valheim's normal fuel use."),
        new(
            "Skills",
            new Color(1f, 0.48f, 0.54f, 1f),
            new[]
            {
                new Entry("Rockbreaker", "Pickaxes adds scaling damage; crits and AOE unlock at level 25"),
                new Entry("Cleave", "After level 25, axe hits can add one half-damage hit to the same tree or log"),
            },
            "Skill-based effects grow through normal play without granting bonus drops."),
        new(
            "Combat",
            new Color(1f, 0.48f, 0.54f, 1f),
            new[]
            {
                new Entry(
                    "Headshots",
                    $"Bow arrows deal ×{HeadshotRules.NearMultiplier:0.##} through {HeadshotRules.NearDistanceMeters:0.#} m, scaling to ×{HeadshotRules.CapMultiplier:0.##} at {HeadshotRules.CapDistanceMeters:0.#} m"),
                new Entry("Adrenaline", "Positive gains are doubled; perfect defenses show the actual gain"),
            },
            "Headshot text confirms local collision-time qualification. Native WeakSpot hits stay native."),
        new(
            "Diagnostics",
            new Color(0.74f, 0.7f, 1f, 1f),
            new[]
            {
                new Entry("/", "Open Valheim's native console when enabled"),
                new Entry("F7", "Save the active Benheim log to the Desktop"),
            },
            "Attach the exported log when reporting behavior another player cannot reproduce."),
    };

    // Valheim's keyboard bindings are single key paths: holding a modifier does
    // not stop the underlying B or P action from firing. Include only Benheim
    // shortcuts that can run while native gameplay input is active. Inventory-
    // only keys are excluded because Player.TakeInput already blocks them.
    private static readonly NativeBinding[] NativeBindings =
    {
        new("Left Shift + B", "Open the Benheim menu", "<Keyboard>/b"),
        new("Left Shift + P", "Put matching items away", "<Keyboard>/p"),
        new("R", "Swap hotbar loadout", "<Keyboard>/r", ignoredNativeAction: "Hide"),
        new("/", "Open Valheim's native console", "<Keyboard>/slash"),
        new("F7", "Save the active Benheim log to the Desktop", "<Keyboard>/f7"),
    };

    private readonly struct Entry
    {
        internal Entry(string key, string action)
        {
            Key = key;
            Action = action;
        }

        internal string Key { get; }
        internal string Action { get; }
    }

    private readonly struct NativeBinding
    {
        internal NativeBinding(string key, string action, string path, string? ignoredNativeAction = null)
        {
            Key = key;
            Action = action;
            Path = path;
            IgnoredNativeAction = ignoredNativeAction;
        }

        internal string Key { get; }
        internal string Action { get; }
        internal string Path { get; }
        internal string? IgnoredNativeAction { get; }
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
