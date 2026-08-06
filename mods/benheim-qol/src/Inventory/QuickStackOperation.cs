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
    internal bool InventoryWasOpen { get; }
    internal int NextContainerIndex { get; set; }
    internal Container? CurrentContainer { get; set; }
    internal int MovedItems { get; set; }
    internal int BusyContainers { get; set; }
    internal QuickStackSummary Summary { get; } = new QuickStackSummary();
}

internal sealed class QuickStackBulkScope
{
    internal static QuickStackBulkScope? Active { get; set; }

    internal QuickStackBulkScope(Player player, Inventory target, Inventory source, QuickStackOperation? operation, Container? container, bool accountsForPutAway)
    {
        Player = player;
        Target = target;
        Source = source;
        Operation = operation;
        Container = container;
        AccountsForPutAway = accountsForPutAway;
    }

    internal Player Player { get; }
    internal Inventory Target { get; }
    internal Inventory Source { get; }
    internal QuickStackOperation? Operation { get; }
    internal Container? Container { get; }
    internal bool AccountsForPutAway { get; }
    internal List<QuickStackItemSnapshot> Items { get; } = new List<QuickStackItemSnapshot>();
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
