using BenheimQoL.Infrastructure;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackDiagnostics
{
    internal static void ItemMoved(
        ItemDrop.ItemData item,
        int moved,
        Container container,
        string containerLocation)
    {
        Diagnostics.Event(
            "Inventory",
            "quick_stack_item",
            $"item={item.m_shared.m_name} moved={moved} container=\"{container.gameObject.name}\" " +
            $"container_id={container.GetInstanceID()} location=\"{containerLocation}\" " +
            $"position=({container.transform.position.x:0.##},{container.transform.position.y:0.##},{container.transform.position.z:0.##})");
    }
}
