using BenheimQoL.Infrastructure;

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

        WorldFeedback.ShowAbovePlayer(player, QuickStackMessages.AbovePlayerSummary(movedItems));
    }
}
