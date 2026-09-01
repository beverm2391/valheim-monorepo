namespace BenheimQoL.Farming;

/// <summary>
/// Owns the session-local mass-planting grid choice. Odd dimensions preserve a
/// single, unambiguous center cell for the native anchor placement.
/// </summary>
internal static class FarmingGridSelection
{
    internal static int CurrentSize { get; private set; } = FarmingSettings.DefaultGridSize;

    internal static bool IsAllowed(int size)
    {
        return size >= FarmingSettings.MinimumGridSize
            && size <= FarmingSettings.MaximumGridSize
            && size % 2 == 1;
    }

    internal static bool TrySelect(int size)
    {
        if (!IsAllowed(size))
        {
            return false;
        }

        CurrentSize = size;
        return true;
    }

    internal static void Reset()
    {
        CurrentSize = FarmingSettings.DefaultGridSize;
    }
}
