using System.Collections.Generic;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackMessages
{
    internal static string NothingMoved(
        int containerCount,
        int skippedPocketed,
        int skippedNoMatchingContainer,
        int skippedFull,
        int skippedBusy)
    {
        List<string> reasons = new List<string>();
        if (skippedPocketed > 0)
        {
            reasons.Add($"{skippedPocketed} pocketed/hotbar");
        }

        if (skippedNoMatchingContainer > 0)
        {
            reasons.Add($"{skippedNoMatchingContainer} no matching chest");
        }

        if (skippedFull > 0)
        {
            reasons.Add($"{skippedFull} chest full");
        }

        if (skippedBusy > 0)
        {
            reasons.Add($"{skippedBusy} chest busy");
        }

        string reasonText = reasons.Count > 0 ? string.Join(", ", reasons) : "no eligible items";
        return $"Nothing moved ({containerCount} chests; {reasonText})";
    }
}
