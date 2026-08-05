using BenheimQoL.Infrastructure;
using BenheimInventoryProtocol;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackAvailability
{
    internal static bool CanRun(Player player, bool inventoryWasOpen)
    {
        if (InventoryTransactions.IsAvailable(out string reason))
        {
            return true;
        }

        Diagnostics.Event(
            "Inventory",
            "quick_stack_rejected",
            $"reason=transaction_protocol_unavailable detail=\"{reason}\"");
        QuickStackFeedback.ShowDetailedResult(
            player,
            inventoryWasOpen,
            reason);
        return false;
    }
}
