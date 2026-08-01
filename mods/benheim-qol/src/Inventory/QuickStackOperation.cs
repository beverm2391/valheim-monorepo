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
    internal QuickStackOperation(Player player, InventoryGui inventoryGui, List<Container> containers)
    {
        Player = player;
        InventoryGui = inventoryGui;
        Containers = containers;
    }

    internal Player Player { get; }
    internal InventoryGui InventoryGui { get; }
    internal List<Container> Containers { get; }
    internal int NextContainerIndex { get; set; }
    internal Container? CurrentContainer { get; set; }
    internal float RequestStartedAt { get; set; }
    internal int MovedItems { get; set; }
    internal int BusyContainers { get; set; }
    internal QuickStackSummary Summary { get; } = new QuickStackSummary();
}
