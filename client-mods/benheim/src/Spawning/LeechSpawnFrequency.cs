using System.Runtime.CompilerServices;

namespace BenheimQoL.Spawning;

internal static class LeechSpawnFrequency
{
    internal const string PrefabName = "Leech";
    internal const float OpportunityMultiplier = 3f;

    internal static float AdjustInterval(float nativeInterval)
    {
        return nativeInterval / OpportunityMultiplier;
    }
}

// SpawnData is a serializable reference type. ConditionalWeakTable gives each
// native object one adjustment marker without retaining unloaded zone objects.
internal sealed class LeechSpawnAdjustmentState<T> where T : class
{
    private readonly ConditionalWeakTable<T, Marker> adjusted = new();

    internal bool TryClaim(T value)
    {
        if (adjusted.TryGetValue(value, out _))
        {
            return false;
        }

        adjusted.Add(value, Marker.Instance);
        return true;
    }

    private sealed class Marker
    {
        internal static readonly Marker Instance = new();
    }
}
