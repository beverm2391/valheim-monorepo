using BenheimQoL.Infrastructure;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackAvailability
{
    internal static bool CanRun(Player player, bool inventoryWasOpen)
    {
        ZNet? network = ZNet.instance;
        bool isTrueSinglePlayer = network != null
            && network.IsServer()
            && !network.IsDedicated()
            && network.GetConnectedPeers().Count == 0;
        if (isTrueSinglePlayer)
        {
            return true;
        }

        Diagnostics.Event(
            "Inventory",
            "quick_stack_rejected",
            "reason=multiplayer_requires_authoritative_transaction");
        QuickStackFeedback.ShowDetailedResult(
            player,
            inventoryWasOpen,
            "Put Away is temporarily unavailable in multiplayer");
        return false;
    }
}
