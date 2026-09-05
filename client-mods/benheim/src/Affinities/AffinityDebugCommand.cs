using System;

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
        if (arguments.Length == 5
            && string.Equals(arguments[3], "apply", StringComparison.OrdinalIgnoreCase))
        {
            AffinityLoadResult selected = arguments[4].ToLowerInvariant() switch
            {
                "lunge" => AffinityLoadResult.Lunge,
                "snipe" => AffinityLoadResult.Snipe,
                "test" => AffinityLoadResult.Test,
                _ => AffinityLoadResult.None,
            };
            if (selected == AffinityLoadResult.None)
            {
                PrintUsage(context);
                return true;
            }
            string name = AffinityPresentation.NameFor(selected);
            AffinityApplicationResult result = AffinityApplication.Apply(
                player,
                weapon,
                selected,
                requireForge: false,
                consumeResources: false,
                source: "debug_apply",
                developerBypass: true);
            context.AddString(result.Applied
                ? $"Applied {name} to the equipped item for development testing."
                : $"Could not apply {name}: {result.Reason}.");
            return true;
        }
        if (arguments.Length == 4
            && string.Equals(arguments[3], "remove", StringComparison.OrdinalIgnoreCase))
        {
            if (weapon == null)
            {
                context.AddString("Equip an item first.");
            }
            else
            {
                bool removed = AffinityState.Clear(weapon, "debug_remove");
                if (removed && player != null)
                {
                    AffinityApplication.NotifyInventoryChanged(player.GetInventory());
                }
                context.AddString(removed
                    ? "Removed the Benheim Affinity from the equipped item."
                    : "The equipped item had no Benheim Affinity state.");
            }
            return true;
        }
        PrintUsage(context);
        return true;
    }

    internal static void PrintUsage(Terminal context)
    {
        context.AddString("  bh debug affinity apply <lunge|snipe|test>");
        context.AddString("  bh debug affinity remove");
    }
}
