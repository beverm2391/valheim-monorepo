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

        if (skippedPocketed > 0)
        {
            string details = reasons.Count > 0
                ? $"; {string.Join(", ", reasons)}"
                : string.Empty;
            return $"{ProtectedWorldText(skippedPocketed)} ({containerCount} chests checked{details})";
        }

        string reasonText = reasons.Count > 0 ? string.Join(", ", reasons) : "no eligible items";
        return $"Nothing moved ({containerCount} chests; {reasonText})";
    }

    internal static string ProtectedWorldText(int count)
    {
        return count == 1 ? "Kept 1 protected stack" : $"Kept {count} protected stacks";
    }
}
