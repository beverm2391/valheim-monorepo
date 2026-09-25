using System;

namespace BenheimQoL.Crafting;

internal static class CraftingBatchRules
{
    internal static int FindMaximumCrafts(int capacityUpperBound, Func<int, bool> isAllowed)
    {
        if (capacityUpperBound < 1 || !isAllowed(1))
        {
            return 0;
        }

        // The inventory gives us a hard upper bound. Binary search keeps the
        // per-frame modifier preview cheap even for large stackable outputs.
        int allowed = 1;
        int denied = capacityUpperBound + 1;
        while (allowed + 1 < denied)
        {
            int candidate = allowed + (denied - allowed) / 2;
            if (isAllowed(candidate))
            {
                allowed = candidate;
            }
            else
            {
                denied = candidate;
            }
        }
        return allowed;
    }

    internal static int FindMaximumCraftsDescending(
        int upperBound,
        Func<int, bool> isAllowed)
    {
        // A one-ingredient recipe can switch which ingredient and quality it
        // selects as the requested count changes. That makes its output amount
        // non-monotonic, so a binary search can skip the real maximum.
        for (int count = Math.Max(0, upperBound); count > 0; count--)
        {
            if (isAllowed(count))
            {
                return count;
            }
        }

        return 0;
    }
}
