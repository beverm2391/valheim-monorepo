using System;
using System.Globalization;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.Affinities;

internal static class AffinityDebugCommand
{
    internal static bool TryExecute(string[] arguments, Terminal context)
    {
        if (arguments.Length < 3
            || !string.Equals(arguments[0], "bh", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(arguments[1], "debug", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(arguments[2], "affinity", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        Player? player = Player.m_localPlayer;
        ItemDrop.ItemData? weapon = player?.GetCurrentWeapon();
        if (arguments.Length == 4
            && string.Equals(arguments[3], "inspect", StringComparison.OrdinalIgnoreCase))
        {
            Inspect(weapon, context);
            return true;
        }
        if (arguments.Length == 5
            && string.Equals(arguments[3], "apply", StringComparison.OrdinalIgnoreCase))
        {
            AffinityLoadResult selected = arguments[4].ToLowerInvariant() switch
            {
                "lunge" => AffinityLoadResult.Lunge,
                "snipe" => AffinityLoadResult.Snipe,
                _ => AffinityLoadResult.None,
            };
            if (selected == AffinityLoadResult.None)
            {
                PrintUsage(context);
                return true;
            }
            string name = AffinityPresentation.NameFor(selected);
            AffinityApplicationResult result = AffinityApplication.Apply(
                player, weapon, selected, requireForge: false, consumeResources: false, source: "debug_apply");
            context.AddString(result.Applied
                ? $"Applied {name} to the equipped item for development testing."
                : $"Could not apply {name}: {result.Reason}.");
            return true;
        }
        if (arguments.Length == 4
            && string.Equals(arguments[3], "clear", StringComparison.OrdinalIgnoreCase))
        {
            if (weapon == null)
            {
                context.AddString("Equip an item first.");
            }
            else
            {
                bool removed = AffinityState.Clear(weapon, "debug_clear");
                if (removed && player != null)
                {
                    AffinityApplication.NotifyInventoryChanged(player.GetInventory());
                }
                context.AddString(removed
                    ? "Cleared Benheim Affinity state from the equipped item."
                    : "The equipped item had no Benheim Affinity state.");
            }
            return true;
        }
        if (arguments.Length == 5
            && string.Equals(arguments[3], "lunge-force", StringComparison.OrdinalIgnoreCase))
        {
            bool parsed = float.TryParse(
                arguments[4],
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float value);
            if (!parsed || !LungeRuntime.TrySetForce(value))
            {
                context.AddString("Usage: bh debug affinity lunge-force <0.01-30>");
            }
            else
            {
                context.AddString($"Session-only Lunge force is now {value.ToString("0.##", CultureInfo.InvariantCulture)}.");
            }
            return true;
        }

        PrintUsage(context);
        return true;
    }

    internal static void PrintUsage(Terminal context)
    {
        context.AddString("  bh debug affinity inspect");
        context.AddString("  bh debug affinity apply lunge");
        context.AddString("  bh debug affinity apply snipe");
        context.AddString("  bh debug affinity clear");
        context.AddString("  bh debug affinity lunge-force <0.01-30>");
    }

    private static void Inspect(ItemDrop.ItemData? weapon, Terminal context)
    {
        AffinityLoadResult state = AffinityState.Load(weapon, "debug_inspect");
        string stored = AffinityState.StoredValue(weapon);
        context.AddString($"Equipped prefab: {AffinityState.ItemPrefab(weapon)}");
        context.AddString($"Eligible max-quality base-game Club: {AffinityState.IsEligibleClub(weapon)}");
        context.AddString($"Eligible max-quality base-game Huntsman Bow: {AffinityState.IsEligibleSnipeBow(weapon)}");
        context.AddString($"Stored Affinity: {state.ToString().ToLowerInvariant()}");
        context.AddString($"Stored identity/version: {(string.IsNullOrEmpty(stored) ? "<none>" : stored)}");
        context.AddString($"Supported identity/version: {AffinityState.LungeValue}, {AffinityState.SnipeValue}");
        string active = HealthReporting.GameplayActionsEnabled
            && state != AffinityLoadResult.None && AffinityState.AvailableFor(weapon) == state
                ? state.ToString().ToLowerInvariant()
                : "native";
        context.AddString($"Active runtime behavior: {active}");
        context.AddString($"Session Lunge force: {LungeRuntime.Force.ToString("0.##", CultureInfo.InvariantCulture)}");
    }
}
