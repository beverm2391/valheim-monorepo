namespace BenheimQoL.InventoryFeature;

internal static class PocketItemController
{
    internal static bool TryTogglePlayerItem(InventoryGrid grid, ItemDrop.ItemData item)
    {
        Player player = Player.m_localPlayer;
        if (item == null || player == null || grid.GetInventory() != player.GetInventory())
        {
            return false;
        }

        if (!PocketItems.Toggle(item, out bool pocketed))
        {
            return false;
        }

        string verb = pocketed ? "Pocketed" : "Unpocketed";
        player.Message(MessageHud.MessageType.TopLeft, $"{verb} {PocketItems.GetDisplayName(item)}");
        return true;
    }
}
