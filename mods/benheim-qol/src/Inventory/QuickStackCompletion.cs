using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static partial class QuickStack
{
    private static void Finish(QuickStackOperation operation)
    {
        activeOperation = null;
        PutAwayLeaseClient.Release("batch_finished");
        InventoryTransactions.BatchFinished(
            operation.OperationId,
            "completed",
            "batch_finished",
            operation.MovedItems,
            PutAwayStageTiming.ElapsedMilliseconds(operation.BatchStartedAt),
            operation.ScanMatchDurationMs);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_finished",
            $"moved={operation.MovedItems} busy_containers={operation.BusyContainers}");
        if (operation.MovedItems > 0)
        {
            operation.InventoryGui.m_moveItemEffects.Create(
                operation.InventoryGui.transform.position,
                Quaternion.identity);
            QuickStackFeedback.ShowDetailedResult(
                operation.Player,
                operation.InventoryWasOpen,
                operation.Summary.Format());
            QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(
                operation.Player,
                operation.InventoryWasOpen,
                operation.MovedItems);
            return;
        }

        QuickStackFeedback.ShowDetailedResult(
            operation.Player,
            operation.InventoryWasOpen,
            QuickStackMessages.NothingMoved(
                operation.Containers.Count,
                0,
                0,
                operation.BusyContainers));
        QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(
            operation.Player,
            operation.InventoryWasOpen,
            movedItems: 0);
    }

    private static void FinishWithNoContainers(
        string operationId,
        long batchStartedAt,
        double scanMatchDurationMs,
        Player player,
        bool inventoryWasOpen)
    {
        PutAwayLeaseClient.Release("no_nearby_containers");
        InventoryTransactions.BatchFinished(
            operationId,
            "completed",
            "no_nearby_containers",
            acceptedCount: 0,
            PutAwayStageTiming.ElapsedMilliseconds(batchStartedAt),
            scanMatchDurationMs);
        Diagnostics.Event("Inventory", "quick_stack_finished", "moved=0 reason=no_nearby_containers");
        QuickStackFeedback.ShowDetailedResult(player, inventoryWasOpen, "No nearby containers");
        QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(
            player,
            inventoryWasOpen,
            movedItems: 0);
    }

    private static void FinishWithNoEligibleContainers(
        string operationId,
        long batchStartedAt,
        double scanMatchDurationMs,
        Player player,
        bool inventoryWasOpen,
        int containerCount,
        QuickStackEligibility eligibility)
    {
        PutAwayLeaseClient.Release("no_eligible_containers");
        InventoryTransactions.BatchFinished(
            operationId,
            "completed",
            "no_eligible_containers",
            acceptedCount: 0,
            PutAwayStageTiming.ElapsedMilliseconds(batchStartedAt),
            scanMatchDurationMs);
        Diagnostics.Event("Inventory", "quick_stack_finished", "moved=0 reason=no_eligible_containers");
        QuickStackFeedback.ShowDetailedResult(
            player,
            inventoryWasOpen,
            QuickStackMessages.NothingMoved(
                containerCount,
                eligibility.SkippedNoMatchingContainer,
                eligibility.SkippedFull,
                skippedBusy: 0));
        QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(
            player,
            inventoryWasOpen,
            movedItems: 0);
    }

    private static string Localize(string name) =>
        Localization.instance != null
            ? Localization.instance.Localize(name)
            : name.TrimStart('$');
}
