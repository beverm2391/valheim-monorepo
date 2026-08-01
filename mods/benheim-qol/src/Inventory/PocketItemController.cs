using BenheimQoL.Infrastructure;

namespace BenheimQoL.InventoryFeature;

internal static class PocketItemController
{
    internal static bool TryTogglePlayerItem(InventoryGrid grid, ItemDrop.ItemData? item)
    {
        Player player = Player.m_localPlayer;
        if (player == null || grid.GetInventory() != player.GetInventory())
        {
            Diagnostics.Event("Inventory", "pocket_toggle_rejected", "reason=not_player_inventory");
            return false;
        }

        if (item == null)
        {
            Diagnostics.Event("Inventory", "pocket_toggle_rejected", "reason=no_hovered_item");
            InventoryFeedback.ShowAbovePlayer(player, "Nothing to pocket");
            return false;
        }

        if (!PocketItems.Toggle(item, out bool pocketed))
        {
            Diagnostics.Event("Inventory", "pocket_toggle_rejected", "reason=missing_item_key");
            return false;
        }

        Diagnostics.Event(
            "Inventory",
            "pocket_toggled",
            $"item={item.m_shared.m_name} pocketed={Diagnostics.Bool(pocketed)}");
        string verb = pocketed ? "Pocketed" : "Unpocketed";
        string message = $"{verb} {PocketItems.GetDisplayName(item)}";
        player.Message(MessageHud.MessageType.TopLeft, message);
        InventoryFeedback.ShowAbovePlayer(player, message);
        return true;
    }
}
