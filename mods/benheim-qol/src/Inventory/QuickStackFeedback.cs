namespace BenheimQoL.InventoryFeature;

internal static class QuickStackFeedback
{
    internal static void ShowAbovePlayerSummaryIfInventoryWasClosed(
        Player player,
        bool inventoryWasOpen,
        int movedItems)
    {
        if (inventoryWasOpen)
        {
            return;
        }

        InventoryFeedback.ShowAbovePlayer(player, QuickStackMessages.AbovePlayerSummary(movedItems));
    }
}
