using System;
using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static partial class QuickStack
{
    private static void FinishScanFailure(
        string operationId,
        long batchStartedAt,
        long scanStartedAt,
        QuickStackStartRequest start,
        Exception exception)
    {
        // The initial scan has no operation and no reserved items. Complete its
        // own lifecycle directly; ResetState cannot emit this batch's terminal.
        PutAwayLeaseClient.Release("container_scan_failed");
        InventoryTransactions.BatchFinished(
            operationId,
            "cancelled",
            "container_scan_failed",
            acceptedCount: 0,
            PutAwayStageTiming.ElapsedMilliseconds(batchStartedAt),
            PutAwayStageTiming.ElapsedMilliseconds(scanStartedAt));
        try
        {
            QuickStackFeedback.ShowDetailedResult(start.Player, start.InventoryWasOpen, ScanFailedMessage);
        }
        finally
        {
            ReportScanFailure(operationId, "container_scan_failed", exception, inFlightCount: 0);
        }
    }

    private static void ReportScanFailure(
        string operationId,
        string reason,
        Exception exception,
        int inFlightCount)
    {
        // Use the protocol's best-effort typed sink so evidence cannot block
        // transaction settlement or retain the lease.
        InventoryTransactions.Emit(
            InventoryTransactionDiagnosticEvent.Create(
                    "put_away_scan_failed", "requester", InventoryTransactionDiagnosticLevel.Warning)
                .Code("operation_id", operationId)
                .Code("operation_phase", "scan")
                .Code("reason", reason)
                .Code("exception_type", exception.GetType().Name)
                .Integer("in_flight", inFlightCount));
        Plugin.Log.LogError($"Put Away scan failed ({reason}): {exception}");
    }

    private static void Finish(
        QuickStackOperation operation,
        QuickStackBatchTerminal terminal)
    {
        if (activeOperation != operation)
        {
            return;
        }

        activeOperation = null;
        PutAwayLeaseClient.Release(terminal.Reason);
        InventoryTransactions.BatchFinished(
            operation.OperationId,
            terminal.Status,
            terminal.Reason,
            operation.MovedItems,
            PutAwayStageTiming.ElapsedMilliseconds(operation.BatchStartedAt),
            operation.ScanMatchDurationMs);
        if (!terminal.Completed)
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_cancelled",
                $"reason={terminal.Reason} moved={operation.MovedItems}");
            if (operation.MovedItems > 0)
            {
                ShowMovedResult(operation);
            }
            return;
        }

        FinishCompleted(operation);
    }

    private static void FinishCompleted(QuickStackOperation operation)
    {
        Diagnostics.Event(
            "Inventory",
            "quick_stack_finished",
            $"moved={operation.MovedItems} busy_containers={operation.BusyContainers}");
        if (operation.MovedItems > 0)
        {
            ShowMovedResult(operation);
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

    private static void ShowMovedResult(QuickStackOperation operation)
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
