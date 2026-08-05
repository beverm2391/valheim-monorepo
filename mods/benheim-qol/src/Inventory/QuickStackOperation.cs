using System.Collections.Generic;

namespace BenheimQoL.InventoryFeature;

internal sealed class QuickStackEligibility
{
    internal List<Container> Containers { get; } = new List<Container>();
    internal int SkippedPocketed { get; set; }
    internal int SkippedNoMatchingContainer { get; set; }
    internal int SkippedFull { get; set; }
}

internal sealed class QuickStackOperation
{
    internal QuickStackOperation(
        Player player,
        InventoryGui inventoryGui,
        List<Container> containers,
        bool inventoryWasOpen)
    {
        Player = player;
        InventoryGui = inventoryGui;
        Containers = containers;
        InventoryWasOpen = inventoryWasOpen;
    }

    internal Player Player { get; }
    internal InventoryGui InventoryGui { get; }
    internal List<Container> Containers { get; }
    internal HashSet<Container> RequestedContainers { get; } = new HashSet<Container>();
    internal bool InventoryWasOpen { get; }
    internal int NextContainerIndex { get; set; }
    internal Container? CurrentContainer { get; set; }
    internal int MovedItems { get; set; }
    internal int BusyContainers { get; set; }
    internal bool ResponseInProgress { get; set; }
    internal bool ResponseGranted { get; set; }
    internal List<QuickStackItemSnapshot> ResponseItems { get; } = new List<QuickStackItemSnapshot>();
    internal QuickStackSummary Summary { get; } = new QuickStackSummary();

    internal bool ContainsResponseItem(ItemDrop.ItemData item)
    {
        foreach (QuickStackItemSnapshot snapshot in ResponseItems)
        {
            if (snapshot.Item == item)
            {
                return true;
            }
        }

        return false;
    }
}

internal sealed class QuickStackItemSnapshot
{
    internal QuickStackItemSnapshot(ItemDrop.ItemData item, int stackBefore)
    {
        Item = item;
        StackBefore = stackBefore;
    }

    internal ItemDrop.ItemData Item { get; }
    internal int StackBefore { get; }
}
