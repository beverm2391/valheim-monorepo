using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using System.Collections.Generic;

namespace BenheimQoL.InventoryFeature;

internal static partial class QuickStack
{
    private static void BeginDepositAfterLeaseValidation(PutAwayLeaseResult leaseResult)
    {
        QuickStackOperation? operation = activeOperation;
        Container? container = operation?.PendingContainer;
        List<DepositCandidate>? candidates = operation?.PendingCandidates;
        if (operation == null
            || operation.OperationId != leaseResult.OperationId)
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_lease_validation_rejected",
                "reason=stale_validation_result");
            return;
        }

        if (!leaseResult.Granted)
        {
            CancelBeforeReservation(operation, leaseResult.Reason);
            return;
        }

        if (!container || candidates == null || !operation.Player)
        {
            CancelBeforeReservation(operation, "validation_context_unavailable");
            return;
        }

        operation.PendingContainer = null;
        operation.PendingCandidates = null;
        operation.CurrentContainer = container;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_request_container",
            $"container=\"{container.gameObject.name}\" items={candidates.Count}");
        if (InventoryTransactions.TryBeginDeposit(
                operation.OperationId,
                operation.Player,
                container,
                candidates,
                result => CompleteContainer(operation, container, result)))
        {
            return;
        }

        operation.CurrentContainer = null;
        operation.BusyContainers++;
        RequestNextContainer();
    }

    private static void CancelBeforeReservation(
        QuickStackOperation operation,
        string reason)
    {
        if (activeOperation != operation)
        {
            return;
        }

        operation.PendingContainer = null;
        operation.PendingCandidates = null;
        activeOperation = null;
        PutAwayLeaseClient.Release(reason);
        InventoryTransactions.BatchFinished(
            operation.OperationId,
            "cancelled",
            reason,
            operation.MovedItems,
            PutAwayStageTiming.ElapsedMilliseconds(operation.BatchStartedAt),
            operation.ScanMatchDurationMs);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_cancelled",
            $"reason={reason} moved={operation.MovedItems}");
        TopLeftFeedbackHud.ShowTransient("Put Away stopped — player compatibility changed");
    }
}
