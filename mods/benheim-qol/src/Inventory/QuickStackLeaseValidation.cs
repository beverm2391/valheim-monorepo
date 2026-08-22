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
        int containerOrder = operation?.PendingContainerOrder ?? -1;
        if (operation == null
            || operation.OperationId != leaseResult.OperationId)
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_lease_validation_rejected",
                "reason=stale_validation_result");
            return;
        }

        if (!container
            || candidates == null
            || containerOrder < 0
            || !operation.Player)
        {
            CancelBeforeReservation(operation, "validation_context_unavailable");
            return;
        }

        if (!leaseResult.Granted)
        {
            CancelBeforeReservation(operation, leaseResult.Reason);
            return;
        }

        operation.PendingContainer = null;
        operation.PendingCandidates = null;
        operation.PendingContainerOrder = -1;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_request_container",
            $"container=\"{container.gameObject.name}\" items={candidates.Count}");
        bool waitForSettlement = QuickStackTransfer.HasLaterCandidateDependency(
            candidates,
            operation.Containers,
            operation.NextContainerIndex);
        QuickStackDepositContinuation continuation = new QuickStackDepositContinuation(
            waitForSettlement,
            () => ContinueScheduling(operation));
        bool began = operation.Pipeline.TryBeginValidatedDeposit(
                callback => InventoryTransactions.TryBeginDeposit(
                    operation.OperationId,
                    operation.Player,
                    container,
                    candidates,
                    callback),
                result => ApplyContainerResult(operation, containerOrder, container, result),
                () => operation.BusyContainers++,
                () => Diagnostics.Event(
                    "Inventory",
                    "quick_stack_duplicate_result",
                    $"container=\"{container.gameObject.name}\""),
                exception =>
                {
                    operation.BusyContainers++;
                    Diagnostics.Event(
                        "Inventory",
                        "quick_stack_container_completion_failed",
                        $"exception={exception.GetType().Name}");
                },
                continuation.DepositSettled);
        continuation.CompleteBegin(began);
        if (began)
        {
            return;
        }

        CancelBeforeReservation(operation, "validation_context_unavailable");
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
        operation.PendingContainerOrder = -1;
        operation.Pipeline.StopScheduling("cancelled", reason);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_scheduling_stopped",
            $"reason={reason} in_flight={operation.Pipeline.InFlightCount}");
        TopLeftFeedbackHud.ShowTransient("Put Away stopped — player compatibility changed");
    }
}
