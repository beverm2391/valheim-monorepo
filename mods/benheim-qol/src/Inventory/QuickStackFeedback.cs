using BenheimQoL.Infrastructure;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackFeedback
{
    internal static void ShowDetailedResult(
        Player player,
        bool inventoryWasOpen,
        string message)
    {
        if (inventoryWasOpen)
        {
            player.Message(MessageHud.MessageType.Center, message);
            return;
        }

        QuickStackReceiptHud.Show(message);
    }

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
