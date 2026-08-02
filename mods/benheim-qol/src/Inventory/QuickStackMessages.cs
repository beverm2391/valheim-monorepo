using System.Collections.Generic;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackMessages
{
    internal static string AbovePlayerSummary(int movedItems)
    {
        if (movedItems <= 0)
        {
            return "Nothing to put away";
        }

        return movedItems == 1 ? "Put away 1 item" : $"Put away {movedItems} items";
    }

    internal static string NothingMoved(
        int containerCount,
        int skippedNoMatchingContainer,
        int skippedFull,
        int skippedBusy)
    {
        List<string> reasons = new List<string>();
        if (skippedNoMatchingContainer > 0)
        {
            reasons.Add($"{skippedNoMatchingContainer} without a matching chest");
        }

        if (skippedFull > 0)
        {
            reasons.Add($"{skippedFull} blocked by full chests");
        }

        if (skippedBusy > 0)
        {
            reasons.Add($"{skippedBusy} busy chests");
        }

        string reasonText = reasons.Count > 0 ? string.Join(", ", reasons) : "no eligible items";
        return $"Nothing moved ({containerCount} chests; {reasonText})";
    }
}
