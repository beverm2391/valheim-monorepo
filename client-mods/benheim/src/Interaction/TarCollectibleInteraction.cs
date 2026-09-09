namespace BenheimQoL.Interaction;

/// <summary>
/// Disables only Valheim's checks that block pickup from tar in the patched
/// pickup methods. Valheim still handles range, ownership, inventory capacity,
/// weight, effects, and all ordinary pickup failures.
/// </summary>
internal static class TarCollectibleInteraction
{
    internal static bool ShouldBlockPickable(Pickable _)
    {
        return false;
    }

    internal static bool ShouldBlockItemDrop(ItemDrop _)
    {
        return false;
    }
}
