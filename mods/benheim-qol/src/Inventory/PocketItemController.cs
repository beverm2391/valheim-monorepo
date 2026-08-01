using BenheimQoL.Infrastructure;

namespace BenheimQoL.InventoryFeature;

internal static class PocketItemController
{
    internal static bool TryTogglePlayerItem(InventoryGrid grid, ItemDrop.ItemData? item)
    {
        Player player = Player.m_localPlayer;
        if (item == null)
        {
            Diagnostics.Event("Inventory", "pocket_toggle_rejected", "reason=no_hovered_item");
            return false;
        }

        if (player == null || grid.GetInventory() != player.GetInventory())
        {
            Diagnostics.Event("Inventory", "pocket_toggle_rejected", "reason=not_player_inventory");
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
        player.Message(MessageHud.MessageType.TopLeft, $"{verb} {PocketItems.GetDisplayName(item)}");
        return true;
    }
}
