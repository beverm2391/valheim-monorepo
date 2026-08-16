using System.Collections.Generic;
using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStack
{
    internal const float Radius = 30f;

    private static QuickStackStartRequest? pendingStart;
    private static QuickStackOperation? activeOperation;

    internal static void Run(Player player, InventoryGui inventoryGui, Container? currentContainer)
    {
        if ((activeOperation != null && (!activeOperation.Player || activeOperation.Player != player))
            || (pendingStart != null && (!pendingStart.Player || pendingStart.Player != player)))
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

    private static void BeginAfterLeaseGranted(string operationId, QuickStackStartRequest start)
    {
        Player player = start.Player;
        List<Container> containers = NearbyContainerIndex.FindAccessibleContainers(player, Radius, start.CurrentContainer);
        Diagnostics.Event("Inventory", "quick_stack_scan", $"containers={containers.Count}");
        if (containers.Count == 0)
        {
            FinishWithNoContainers(player, start.InventoryWasOpen);
            return;
        }

        QuickStackEligibility eligibility = QuickStackTransfer.FindEligibleContainers(player, containers);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_eligibility",
            $"eligible_containers={eligibility.Containers.Count} pocketed={eligibility.SkippedPocketed} " +
            $"no_match={eligibility.SkippedNoMatchingContainer} full={eligibility.SkippedFull}");
        if (eligibility.Containers.Count == 0)
        {
            FinishWithNoEligibleContainers(player, start.InventoryWasOpen, containers.Count, eligibility);
            return;
        }

        activeOperation = new QuickStackOperation(
            operationId,
            player,
            start.InventoryGui,
            eligibility.Containers,
            start.InventoryWasOpen);
        RequestNextContainer();
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
            Container container = operation.Containers[operation.NextContainerIndex++];
            if (!container)
            {
                continue;
            }

            List<DepositCandidate> candidates = QuickStackTransfer.FindCandidates(operation.Player, container);
            if (candidates.Count == 0)
            {
                continue;
            }

            operation.CurrentContainer = container;
            Diagnostics.Event(
                "Inventory",
                "quick_stack_request_container",
                $"container=\"{container.gameObject.name}\" index={operation.NextContainerIndex}/{operation.Containers.Count} " +
                $"items={candidates.Count}");
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
        }

        Finish(operation);
    }

    private static void CompleteContainer(
        QuickStackOperation operation,
        Container container,
        DepositResult result)
    {
        if (activeOperation != operation || operation.CurrentContainer != container)
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_stale_result",
                $"container=\"{container.gameObject.name}\" status={result.Status}");
            return;
        }

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

        operation.CurrentContainer = null;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_container_result",
            $"container=\"{container.gameObject.name}\" status={result.Status} moved={movedItems}");
        RequestNextContainer();
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
            HandleLeaseResult(leaseResult!);
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
        BeginAfterLeaseGranted(result.OperationId, start);
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

    private static void Finish(QuickStackOperation operation)
    {
        activeOperation = null;
        PutAwayLeaseClient.Release("batch_finished");
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
            QuickStackMessages.NothingMoved(operation.Containers.Count, 0, 0, operation.BusyContainers));
        QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(
            operation.Player,
            operation.InventoryWasOpen,
            movedItems: 0);
    }

    private static void FinishWithNoContainers(Player player, bool inventoryWasOpen)
    {
        PutAwayLeaseClient.Release("no_nearby_containers");
        Diagnostics.Event("Inventory", "quick_stack_finished", "moved=0 reason=no_nearby_containers");
        QuickStackFeedback.ShowDetailedResult(player, inventoryWasOpen, "No nearby containers");
        QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(player, inventoryWasOpen, movedItems: 0);
    }

    private static void FinishWithNoEligibleContainers(
        Player player,
        bool inventoryWasOpen,
        int containerCount,
        QuickStackEligibility eligibility)
    {
        PutAwayLeaseClient.Release("no_eligible_containers");
        Diagnostics.Event("Inventory", "quick_stack_finished", "moved=0 reason=no_eligible_containers");
        QuickStackFeedback.ShowDetailedResult(
            player,
            inventoryWasOpen,
            QuickStackMessages.NothingMoved(
                containerCount,
                eligibility.SkippedNoMatchingContainer,
                eligibility.SkippedFull,
                skippedBusy: 0));
        QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(player, inventoryWasOpen, movedItems: 0);
    }

    private static string Localize(string name)
    {
        return Localization.instance != null ? Localization.instance.Localize(name) : name.TrimStart('$');
    }

}
