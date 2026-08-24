using System;
using System.Collections.Generic;
using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static partial class QuickStack
{
    internal const float Radius = 30f;

    private static QuickStackStartRequest? pendingStart;
    private static QuickStackOperation? activeOperation;

    internal static void Run(Player player, InventoryGui inventoryGui, Container? currentContainer)
    {
        bool playerContextChanged =
            (activeOperation != null && (!activeOperation.Player || activeOperation.Player != player))
            || (pendingStart != null && (!pendingStart.Player || pendingStart.Player != player));
        if (playerContextChanged
            && !InventoryTransactionLifecyclePolicy.CanResetBatch(
                InventoryTransactions.HasUnsettledClientDeposit))
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_rejected",
                "reason=transaction_settlement_in_progress");
            TopLeftFeedbackHud.ShowTransient("Put Away already in progress");
            return;
        }

        if (playerContextChanged)
        {
            ResetState();
        }

        bool inventoryWasOpen = InventoryVisibility.IsOpen(inventoryGui);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_requested",
            $"radius={Radius:0.#} inventory_open={Diagnostics.Bool(inventoryWasOpen)}");
        if (activeOperation != null || pendingStart != null || PutAwayLeaseClient.IsPendingOrHeld)
        {
            Diagnostics.Event("Inventory", "quick_stack_rejected", "reason=already_in_progress");
            TopLeftFeedbackHud.ShowTransient("Put Away already in progress");
            return;
        }

        if (!PutAwayLeaseClient.TryRequest(Time.unscaledTime, out string reason))
        {
            Diagnostics.Event("Inventory", "quick_stack_rejected", $"reason={reason}");
            TopLeftFeedbackHud.ShowTransient("Put Away unavailable — compatible server required");
            return;
        }

        pendingStart = new QuickStackStartRequest(
            player,
            inventoryGui,
            currentContainer,
            inventoryWasOpen);
    }

    private static void BeginAfterLeaseGranted(
        string operationId,
        long batchStartedAt,
        QuickStackStartRequest start)
    {
        Player player = start.Player;
        long scanMatchStartedAt = PutAwayStageTiming.Start();
        List<Container> containers = NearbyContainerIndex.FindAccessibleContainers(player, Radius, start.CurrentContainer);
        double scanMatchDurationMs = PutAwayStageTiming.ElapsedMilliseconds(scanMatchStartedAt);
        Diagnostics.Event("Inventory", "quick_stack_scan", $"containers={containers.Count}");
        if (containers.Count == 0)
        {
            FinishWithNoContainers(
                operationId,
                batchStartedAt,
                scanMatchDurationMs,
                player,
                start.InventoryWasOpen);
            return;
        }

        scanMatchStartedAt = PutAwayStageTiming.Start();
        QuickStackEligibility eligibility = QuickStackTransfer.FindEligibleContainers(player, containers);
        scanMatchDurationMs += PutAwayStageTiming.ElapsedMilliseconds(scanMatchStartedAt);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_eligibility",
            $"eligible_containers={eligibility.Containers.Count} pocketed={eligibility.SkippedPocketed} " +
            $"no_match={eligibility.SkippedNoMatchingContainer} full={eligibility.SkippedFull}");
        if (eligibility.Containers.Count == 0)
        {
            FinishWithNoEligibleContainers(
                operationId,
                batchStartedAt,
                scanMatchDurationMs,
                player,
                start.InventoryWasOpen,
                containers.Count,
                eligibility);
            return;
        }

        QuickStackOperation? operation = null;
        operation = new QuickStackOperation(
            operationId,
            batchStartedAt,
            player,
            start.InventoryGui,
            eligibility.Containers,
            start.InventoryWasOpen,
            scanMatchDurationMs,
            terminal => Finish(operation!, terminal));
        activeOperation = operation;
        ContinueScheduling(operation);
    }

    private static void RequestNextContainer()
    {
        QuickStackOperation? operation = activeOperation;
        if (operation == null)
        {
            return;
        }

        while (operation.NextContainerIndex < operation.Containers.Count)
        {
            int containerOrder = operation.NextContainerIndex++;
            Container container = operation.Containers[containerOrder];
            if (!container)
            {
                continue;
            }

            long scanMatchStartedAt = PutAwayStageTiming.Start();
            List<DepositCandidate> candidates = QuickStackTransfer.FindCandidates(operation.Player, container);
            operation.ScanMatchDurationMs += PutAwayStageTiming.ElapsedMilliseconds(scanMatchStartedAt);
            if (candidates.Count == 0)
            {
                continue;
            }

            operation.PendingContainer = container;
            operation.PendingCandidates = candidates;
            operation.PendingContainerOrder = containerOrder;
            Diagnostics.Event(
                "Inventory",
                "quick_stack_validate_container",
                $"container=\"{container.gameObject.name}\" index={operation.NextContainerIndex}/{operation.Containers.Count} " +
                $"items={candidates.Count}");
            string validationReason = string.Empty;
            if (operation.Pipeline.TryRequestValidation(() =>
                    PutAwayLeaseClient.TryValidate(
                        operation.OperationId,
                        Time.unscaledTime,
                        out validationReason)))
            {
                return;
            }

            CancelBeforeReservation(operation, validationReason);
            return;
        }

        operation.Pipeline.StopScheduling("completed", "batch_finished");
    }

    private static void ApplyContainerResult(
        QuickStackOperation operation,
        int containerOrder,
        Container container,
        DepositResult result)
    {
        string containerDisplayName = Localize(container.GetHoverName());
        string containerLocation = QuickStackLocation.Format(operation.Player, container);
        int movedItems = 0;
        foreach (DepositResultEntry entry in result.Entries)
        {
            if (entry.Accepted <= 0)
            {
                continue;
            }

            movedItems += entry.Accepted;
            operation.Summary.Add(
                container.GetInstanceID(),
                containerOrder,
                containerDisplayName,
                containerLocation,
                Localize(entry.Item.m_shared.m_name),
                entry.Accepted);
            QuickStackDiagnostics.ItemMoved(
                operation.OperationId,
                entry.Item,
                entry.Accepted,
                container,
                containerLocation);
        }

        operation.MovedItems += movedItems;
        if (!result.Succeeded)
        {
            operation.BusyContainers++;
        }

        Diagnostics.Event(
            "Inventory",
            "quick_stack_container_result",
            $"container=\"{container.gameObject.name}\" status={result.Status} moved={movedItems}");
    }

    private static void ContinueScheduling(QuickStackOperation operation)
    {
        try
        {
            RequestNextContainer();
        }
        catch (Exception exception)
        {
            // Reservations already handed to the transaction protocol remain
            // authoritative. Stop issuing new work, then keep the lease until
            // every existing ticket settles.
            operation.Pipeline.StopScheduling("cancelled", "container_scheduling_failed");
            Diagnostics.Event(
                "Inventory",
                "quick_stack_scheduling_failed",
                $"exception={exception.GetType().Name} in_flight={operation.Pipeline.InFlightCount}");
        }
    }

    internal static QuickStackBulkScope? BeginBulkStack(Inventory target, Inventory source)
    {
        QuickStackOperation? operation = activeOperation;
        Player? player = Player.m_localPlayer;
        if (!player || source != player.GetInventory())
        {
            return null;
        }

        QuickStackBulkScope scope = new QuickStackBulkScope(
            player,
            target,
            QuickStackBulkScope.Active);
        QuickStackBulkScope.Active = scope;
        return scope;
    }

    internal static bool ShouldRunBulkStack(QuickStackBulkScope? scope) => true;

    internal static bool ShouldAllowNativeAdd(Inventory target, ItemDrop.ItemData item)
    {
        QuickStackBulkScope? scope = QuickStackBulkScope.Active;
        if (scope == null || scope.Target != target || !PocketItems.IsPocketed(scope.Player, item))
        {
            return true;
        }

        Diagnostics.Event("Inventory", "quick_stack_item_skipped", $"item={item.m_shared.m_name} reason=pocketed");
        return false;
    }

    internal static void CompleteBulkStack(QuickStackBulkScope? scope)
    {
        if (scope == null)
        {
            return;
        }

        RestoreBulkScope(scope);
    }

    internal static System.Exception? FinalizeBulkStack(QuickStackBulkScope? scope, System.Exception? exception)
    {
        RestoreBulkScope(scope);
        return exception;
    }

    internal static void ResetState()
    {
        bool hasUnsettledDeposit = InventoryTransactions.HasUnsettledClientDeposit;
        if (!InventoryTransactionLifecyclePolicy.CanResetBatch(hasUnsettledDeposit))
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_reset_deferred",
                "reason=transaction_settlement_in_progress");
            return;
        }

        if (activeOperation != null)
        {
            InventoryTransactions.BatchFinished(
                activeOperation.OperationId,
                "cancelled",
                "client_reset",
                activeOperation.MovedItems,
                PutAwayStageTiming.ElapsedMilliseconds(activeOperation.BatchStartedAt),
                activeOperation.ScanMatchDurationMs);
        }
        pendingStart = null;
        activeOperation = null;
        QuickStackBulkScope.Active = null;
        PutAwayLeaseClient.Reset();
    }

    internal static void Update()
    {
        PutAwayLeaseClient.Update(Time.unscaledTime);
        if (PutAwayLeaseClient.TryTakeResult(out PutAwayLeaseResult? leaseResult))
        {
            if (leaseResult!.IsValidation)
            {
                BeginDepositAfterLeaseValidation(leaseResult);
            }
            else
            {
                HandleLeaseResult(leaseResult);
            }
        }
    }

    private static void HandleLeaseResult(PutAwayLeaseResult result)
    {
        QuickStackStartRequest? start = pendingStart;
        pendingStart = null;
        if (!result.Granted)
        {
            Diagnostics.Event("Inventory", "quick_stack_rejected", $"reason={result.Reason}");
            string message = result.Reason == "busy"
                ? "Put Away busy — retry in a few seconds"
                : "Put Away unavailable — compatible server required";
            TopLeftFeedbackHud.ShowTransient(message);
            return;
        }

        if (start == null || !start.Player)
        {
            PutAwayLeaseClient.Release("start_context_unavailable");
            Diagnostics.Event("Inventory", "quick_stack_cancelled", "reason=start_context_unavailable");
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("Inventory", "quick_stack_lease_entered")
                .String("operation_id", result.OperationId)
                .String("operation_phase", "mutation_allowed"));
        long batchStartedAt = PutAwayStageTiming.Start();
        InventoryTransactions.BatchStarted(result.OperationId);
        BeginAfterLeaseGranted(result.OperationId, batchStartedAt, start);
    }

    private static void RestoreBulkScope(QuickStackBulkScope? scope)
    {
        if (scope != null && QuickStackBulkScope.Active == scope)
        {
            QuickStackBulkScope.Active = scope.Previous;
        }
    }

    internal static bool ShouldSuppressNativeStackMessage(MessageHud.MessageType type, string message)
    {
        // Put Away no longer invokes requester-local StackAll. Ordinary native
        // Stack All messages therefore remain untouched.
        return false;
    }

}
