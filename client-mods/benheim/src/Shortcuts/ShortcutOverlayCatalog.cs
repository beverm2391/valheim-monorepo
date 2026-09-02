using BenheimQoL.Affinities;
using BenheimQoL.Archery;
using BenheimQoL.Farming;
using BenheimQoL.InventoryFeature;
using BenheimQoL.KillAttribution;
using BenheimQoL.PlayerCombat;
using BenheimQoL.Repair;
using BenheimQoL.ShipSprint;
using BenheimQoL.WeaponRhythm;
using BenheimQoL.WorldLabels;
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
                new Entry("Left Shift + (1 / 3 / 5 / 7 / 9)", "Choose the planting grid while the Cultivator picker is open"),
                new Entry("Left Shift + plant", $"Plant the centered selected grid (defaults to {FarmingSettings.DefaultGridSize}x{FarmingSettings.DefaultGridSize} each time the picker opens)"),
                new Entry("Cultivator berries", $"Plant native Raspberry, Blueberry, and Cloudberry bushes for {PlantableBerries.BerryCost} matching berries each"),
            },
            "Each successful ordinary or grid plant placement costs 25% of the native planting stamina cost that Valheim has already resolved. Skipped, failed, and rejected placements cost no stamina. Apart from the selected odd grid dimensions and stamina cost, all other planting behavior stays native. Berry bushes need ordinary ground. They do not need cultivated ground or a matching biome. Newly planted bushes start empty until their native growth cycle completes. Every grid uses each native bush's collider footprint for spacing."),
    };

    private static readonly Section[] FeatureSections =
    {
        new(
            "Building",
            new Color(1f, 0.58f, 0.36f, 1f),
            new[]
            {
                new Entry(
                    "Station coverage",
                    "Workbench and Stonecutter build-piece placement coverage is 2× Valheim's native range (20 m to 40 m for level-1 stations)"),
            },
            "Crafting, repair, station interaction, Workbench suppression, enemy spawning, and all other station behavior stay native."),
        new(
            "World & Travel",
            TravelAccent,
            new[]
            {
                new Entry("Extended reach", "Use interactable objects from up to 8 m; open containers stay available to 10 m"),
                new Entry("Tar pickup", "Manually collect native Tar while submerged; auto-pickup and other submerged items remain stuck"),
                new Entry("Portal travel", "Finish the transition sooner after the destination is ready"),
                new Entry("Glowing signs", "Existing sign letters have a soft, warm portal-amber glow. The wooden board stays unchanged"),
                new Entry(
                    "Portal labels",
                    $"Show each portal's exact non-empty tag above it when the portal is within {WorldLabelVisibility.PortalMaxDistanceMeters:0} m and in the player's line of sight"),
                new Entry(
                    "Ship Sprint",
                    $"Hold Run at the helm for ×{ShipSprintTuning.ThrustMultiplier:0.#} native thrust at paddle, half sail, and full sail; the helm readout shows planar speed and marks SPRINT while requested"),
            },
            "These features reduce waiting and positioning friction without automating play. Every possible ship physics owner needs compatible Benheim."),
        new(
            "Production",
            new Color(1f, 0.58f, 0.36f, 1f),
            new[]
            {
                new Entry("Stone Oven", "Baking and done-to-burn timing are halved; fuel stays normal"),
            },
            "Faster baking preserves Valheim's normal fuel use."),
        new(
            "Gathering & Skills",
            new Color(1f, 0.48f, 0.54f, 1f),
            new[]
            {
                new Entry("Rockbreaker", "Pickaxes adds scaling damage; crits and AOE unlock at level 25"),
                new Entry("Cleave", "After level 25, axe hits can add one half-damage hit to the same tree or log"),
                new Entry(
                    "Finewood",
                    "Native Birch and Oak logs convert each final ordinary Wood drop to Finewood without changing each log's native item count or Valheim's spawn path"),
            },
            "The compatible client that owns the log converts its drops, including when another compatible client attacks. Native Finewood and non-Wood drops, other logs, standing-tree drops, stumps, damage-type conversions, and unrelated destruction stay native."),
        new(
            "Affinities",
            new Color(0.86f, 0.54f, 1f, 1f),
            new[]
            {
                new Entry(
                    "Club + Lunge",
                    $"At a Forge, spend {AffinityApplication.TestResourceAmount} Wood to bind Lunge to one exact max-quality Club; the Affinity stays with that item"),
                new Entry(
                    "Airborne swing",
                    $"A primary swing while airborne adds {LungeRuntime.DefaultForce:0.#} m/s forward and raises vertical velocity to at least +{LungeRuntime.MinimumVerticalVelocity:0.#} m/s; grounded Club swings stay native"),
            },
            "Replacing an Affinity consumes the new cost and destroys the old Affinity and its prior investment without a refund."),
        new(
            "Combat",
            new Color(1f, 0.48f, 0.54f, 1f),
            new[]
            {
                new Entry(
                    "Headshots",
                    $"Bow arrows deal ×{HeadshotRules.NearMultiplier:0.##} through {HeadshotRules.NearDistanceMeters:0.#} m, scaling to ×{HeadshotRules.CapMultiplier:0.##} at {HeadshotRules.CapDistanceMeters:0.#} m"),
                new Entry(
                    "Perfect Impact",
                    $"While airborne, descend at least {-AirborneMeleeTuning.DescentThreshold:0.#} m/s and approach the contact horizontally at {AirborneMeleeTuning.ApproachSpeedThreshold:0.#} m/s: ×{AirborneMeleeTuning.DamageMultiplier:0.##} damage and ×{AirborneMeleeTuning.StaggerMultiplier:0.#} stagger"),
                new Entry("Adrenaline", "Positive gains are doubled; perfect defenses show the actual gain"),
                new Entry(
                    "CLUTCH",
                    $"Perfect parry or dodge below {ClutchMechanic.HealthThreshold:0} health: recover 60 health over {ClutchMechanic.DurationSeconds:0} seconds"),
                new Entry(
                    "UNTOUCHABLE",
                    "At 5, 8, and 12 streak points from perfect defenses or qualifying kills: +10%, +20%, or +30% outgoing damage until actual health loss"),
                new Entry(
                    "BERSERKER",
                    $"At {KillChainRules.BerserkerKillThreshold} qualifying kills: 25% physical resistance and +50% stamina regeneration"),
                new Entry(
                    "SLAUGHTERHOUSE",
                    $"At {KillChainRules.SlaughterhouseKillThreshold} qualifying kills: 50% physical resistance and +100% stamina regeneration"),
                new Entry(
                    "Earned-state cue",
                    "Compatible nearby players may hear the native charm audio; distant players cannot"),
            },
            $"Each qualifying kill resets the {KillChainRules.WindowSeconds:0}-second BERSERKER timer. " +
                "BERSERKER, SLAUGHTERHOUSE, and kill-based UNTOUCHABLE progression require Benheim Server Support. " +
                "PERFECT IMPACT appears at the target even when FX is off; FX settings gate only its shake. " +
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
