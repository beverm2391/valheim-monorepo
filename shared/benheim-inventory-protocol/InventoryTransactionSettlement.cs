using System;
using System.Collections.Generic;

namespace BenheimInventoryProtocol;

internal enum InventoryTransactionRefundPlacement
{
    OriginalSlot,
    Inventory,
    WorldDrop
}

internal static class InventoryTransactionRefundPolicy
{
    internal static InventoryTransactionRefundPlacement Decide(
        bool restoredToOriginalSlot,
        bool restoredElsewhere)
    {
        if (restoredToOriginalSlot)
        {
            return InventoryTransactionRefundPlacement.OriginalSlot;
        }

        return restoredElsewhere
            ? InventoryTransactionRefundPlacement.Inventory
            : InventoryTransactionRefundPlacement.WorldDrop;
    }
}

internal static class InventoryTransactionLifecyclePolicy
{
    internal static bool CanSettle(bool localPlayerAvailable) => localPlayerAvailable;

    internal static bool CanResetBatch(bool hasUnsettledDeposit) => !hasUnsettledDeposit;
}

internal sealed class InventoryTransactionSettlement
{
    private InventoryTransactionSettlement(int[] accepted, int[] rejected)
    {
        Accepted = accepted;
        Rejected = rejected;
    }

    internal IReadOnlyList<int> Accepted { get; }
    internal IReadOnlyList<int> Rejected { get; }

    internal static bool TryCreate(
        IReadOnlyList<int> reserved,
        IReadOnlyList<int> reportedAccepted,
        out InventoryTransactionSettlement? settlement)
    {
        // Every terminal result needs one accepted count per reservation. A
        // non-success status can still contain positive accepted amounts after
        // a partial native write, so treating a short vector as all-zero would
        // refund items that the owner already committed.
        if (reportedAccepted.Count != reserved.Count)
        {
            settlement = null;
            return false;
        }

        int[] accepted = new int[reserved.Count];
        int[] rejected = new int[reserved.Count];
        for (int index = 0; index < reserved.Count; index++)
        {
            int reported = reportedAccepted[index];
            accepted[index] = Math.Max(0, Math.Min(reported, reserved[index]));
            rejected[index] = reserved[index] - accepted[index];
        }

        settlement = new InventoryTransactionSettlement(accepted, rejected);
        return true;
    }
}
