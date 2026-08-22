using System;
using System.Collections.Generic;
using BenheimInventoryProtocol;

namespace BenheimQoL.InventoryFeature;

internal sealed class QuickStackEligibility
{
    internal List<Container> Containers { get; } = new List<Container>();
    internal int SkippedPocketed { get; set; }
    internal int SkippedNoMatchingContainer { get; set; }
    internal int SkippedFull { get; set; }
}

internal sealed class QuickStackStartRequest
{
    internal QuickStackStartRequest(
        Player player,
        InventoryGui inventoryGui,
        Container? currentContainer,
        bool inventoryWasOpen)
    {
        Player = player;
        InventoryGui = inventoryGui;
        CurrentContainer = currentContainer;
        InventoryWasOpen = inventoryWasOpen;
    }

    internal Player Player { get; }
    internal InventoryGui InventoryGui { get; }
    internal Container? CurrentContainer { get; }
    internal bool InventoryWasOpen { get; }
}

internal sealed class QuickStackOperation
{
    internal QuickStackOperation(
        string operationId,
        long batchStartedAt,
        Player player,
        InventoryGui inventoryGui,
        List<Container> containers,
        bool inventoryWasOpen,
        double scanMatchDurationMs,
        Action<QuickStackBatchTerminal> terminalReady)
    {
        OperationId = operationId;
        BatchStartedAt = batchStartedAt;
        Player = player;
        InventoryGui = inventoryGui;
        Containers = containers;
        InventoryWasOpen = inventoryWasOpen;
        ScanMatchDurationMs = scanMatchDurationMs;
        Pipeline = new QuickStackBatchPipeline<DepositResult>(terminalReady);
    }

    internal string OperationId { get; }
    internal long BatchStartedAt { get; }
    internal Player Player { get; }
    internal InventoryGui InventoryGui { get; }
    internal List<Container> Containers { get; }
    internal bool InventoryWasOpen { get; }
    internal int NextContainerIndex { get; set; }
    internal Container? PendingContainer { get; set; }
    internal List<DepositCandidate>? PendingCandidates { get; set; }
    internal int PendingContainerOrder { get; set; } = -1;
    internal int MovedItems { get; set; }
    internal int BusyContainers { get; set; }
    internal double ScanMatchDurationMs { get; set; }
    internal QuickStackBatchPipeline<DepositResult> Pipeline { get; }
    internal QuickStackSummary Summary { get; } = new QuickStackSummary();
}

internal sealed class QuickStackBulkScope
{
    internal static QuickStackBulkScope? Active { get; set; }

    internal QuickStackBulkScope(
        Player player,
        Inventory target,
        QuickStackBulkScope? previous)
    {
        Player = player;
        Target = target;
        Previous = previous;
    }

    internal Player Player { get; }
    internal Inventory Target { get; }
    internal QuickStackBulkScope? Previous { get; }
}
