namespace BenheimQoL.Farming;

/// <summary>
/// Owns the session-local mass-planting grid choice. Odd dimensions preserve a
/// single, unambiguous center cell for the native anchor placement.
/// </summary>
internal static class FarmingGridSelection
{
    private static bool pickerWasOpen;

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

    internal static bool UpdatePickerSession(bool pickerOpen)
    {
        if (!pickerOpen)
        {
            pickerWasOpen = false;
            return false;
        }

        if (pickerWasOpen)
        {
            return false;
        }

        pickerWasOpen = true;
        CurrentSize = FarmingSettings.DefaultGridSize;
        return true;
    }

    internal static void Reset()
    {
        pickerWasOpen = false;
        CurrentSize = FarmingSettings.DefaultGridSize;
    }
}
