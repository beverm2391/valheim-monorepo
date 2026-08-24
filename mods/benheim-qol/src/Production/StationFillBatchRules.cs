using System;
using System.Collections.Generic;

namespace BenheimQoL.Production;

internal static class StationFillBatchRules
{
    internal static bool UsesOwnerBatch(bool stationIsLocalOwner)
    {
        return !stationIsLocalOwner;
    }

    internal static int FirstAvailableIndex(IReadOnlyList<int> materialCounts)
    {
        for (int index = 0; index < materialCounts.Count; index++)
        {
            if (materialCounts[index] > 0)
            {
                return index;
            }
        }
        return -1;
    }

    internal static int RequestedCount(int materialCount, int capacity)
    {
        return Math.Max(0, Math.Min(materialCount, capacity));
    }

    internal static int AcceptedCount(
        float liveLevel,
        float capacity,
        int requested,
        bool inputAllowed)
    {
        if (!inputAllowed || requested <= 0 || capacity <= 0f ||
            float.IsNaN(liveLevel) || float.IsNaN(capacity))
        {
            return 0;
        }

        int available = Math.Max(0, (int)Math.Floor(capacity - liveLevel));
        return Math.Min(requested, available);
    }
}
