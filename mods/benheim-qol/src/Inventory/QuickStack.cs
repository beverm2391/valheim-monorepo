using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStack
{
    internal const float Radius = 30f;

    private static QuickStackOperation? activeOperation;
    private static readonly QuickStackResponseGuard<Container> ResponseGuard = new QuickStackResponseGuard<Container>();
    internal static void Run(Player player, InventoryGui inventoryGui, Container? currentContainer)
    {
        if (activeOperation != null && (!activeOperation.Player || activeOperation.Player != player))
        {
            ResetState();
        }

        bool inventoryWasOpen = InventoryVisibility.IsOpen(inventoryGui);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_requested",
            $"radius={Radius:0.#} inventory_open={Diagnostics.Bool(inventoryWasOpen)}");
        if (activeOperation != null)
        {
            Diagnostics.Event("Inventory", "quick_stack_rejected", "reason=already_in_progress");
            TopLeftFeedbackHud.ShowTransient("Put Away already in progress");
            return;
        }

        List<Container> containers = NearbyContainerIndex.FindAccessibleContainers(player, Radius, currentContainer);
        Diagnostics.Event("Inventory", "quick_stack_scan", $"containers={containers.Count}");
        if (containers.Count == 0)
        {
            FinishWithNoContainers(player, inventoryWasOpen);
            return;
        }

        QuickStackEligibility eligibility = FindEligibleContainers(player, containers);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_eligibility",
            $"eligible_containers={eligibility.Containers.Count} pocketed={eligibility.SkippedPocketed} " +
            $"no_match={eligibility.SkippedNoMatchingContainer} full={eligibility.SkippedFull}");
        if (eligibility.Containers.Count == 0)
        {
            FinishWithNoEligibleContainers(player, inventoryWasOpen, containers.Count, eligibility);
            return;
        }

        activeOperation = new QuickStackOperation(
            player,
            inventoryGui,
            eligibility.Containers,
            inventoryWasOpen);
        RequestNextContainer();
    }

    private static QuickStackEligibility FindEligibleContainers(Player player, List<Container> containers)
    {
        QuickStackEligibility eligibility = new QuickStackEligibility();
        HashSet<Container> seen = new HashSet<Container>();
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItemsInGridOrder())
        {
            if (item == null || item.m_stack <= 0)
            {
                continue;
            }

            if (PocketItems.IsPocketed(player, item))
            {
                eligibility.SkippedPocketed++;
                continue;
            }

            bool foundMatch = false;
            bool foundRoom = false;
            foreach (Container container in containers)
            {
                Inventory target = container.GetInventory();
                if (!target.ContainsItemByName(item.m_shared.m_name))
                {
                    continue;
                }

                foundMatch = true;
                if (!target.CanAddItem(item, 1))
                {
                    continue;
                }

                foundRoom = true;
                seen.Add(container);
            }

            if (!foundMatch)
            {
                eligibility.SkippedNoMatchingContainer++;
            }
            else if (!foundRoom)
            {
                eligibility.SkippedFull++;
            }
        }

        // NearbyContainerIndex has already ordered this list nearest-first. Preserve that
        // order even when a farther chest happens to match the first inventory item.
        foreach (Container container in containers)
        {
            if (seen.Contains(container))
            {
                eligibility.Containers.Add(container);
            }
        }

        return eligibility;
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

            if (ResponseGuard.IsWaitingForTimedOutResponse(container))
            {
                Diagnostics.Event(
                    "Inventory",
                    "quick_stack_container_skipped",
                    $"container=\"{container.gameObject.name}\" reason=awaiting_timed_out_response");
                continue;
            }

            int candidates = CountCandidates(operation.Player, container);
            if (candidates == 0)
            {
                continue;
            }

            if (!ResponseGuard.TryBeginRequest(container, Time.unscaledTime))
            {
                Diagnostics.Event("Inventory", "quick_stack_container_skipped", $"container=\"{container.gameObject.name}\" reason=response_guard_busy");
                continue;
            }

            operation.CurrentContainer = container;
            Diagnostics.Event(
                "Inventory",
                "quick_stack_request_container",
                $"container=\"{container.gameObject.name}\" index={operation.NextContainerIndex}/{operation.Containers.Count} " +
                $"items={candidates}");
            container.StackAll();
            return;
        }

        Finish(operation);
    }

    private static int CountCandidates(Player player, Container container)
    {
        Inventory target = container.GetInventory();
        int candidates = 0;
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItemsInGridOrder())
        {
            if (item != null
                && item.m_stack > 0
                && !PocketItems.IsPocketed(player, item)
                && target.ContainsItemByName(item.m_shared.m_name)
                && target.CanAddItem(item, 1))
            {
                candidates++;
            }
        }

        return candidates;
    }

    internal static bool TryHandleNativeDenial(Container container)
    {
        QuickStackOperation? operation = activeOperation;
        if (operation == null || operation.CurrentContainer != container)
        {
            return false;
        }

        ResponseGuard.CompleteCurrentResponse(container);
        operation.CurrentContainer = null;
        operation.BusyContainers++;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_container_result",
            $"container=\"{container.gameObject.name}\" status=denied moved=0");
        RequestNextContainer();
        return true;
    }

    internal static QuickStackBulkScope? BeginBulkStack(Inventory target, Inventory source)
    {
        QuickStackOperation? operation = activeOperation;
        Player? player = Player.m_localPlayer;
        if (!player || source != player.GetInventory())
        {
            return null;
        }

        Container? container = operation?.CurrentContainer;
        // Every bulk stack now uses the same protection rule. Any StackAll into the
        // active chest is therefore equivalent to its granted Put Away response and may
        // complete this step without tracking which UI action originated the request.
        bool accountsForPutAway = operation != null
            && operation.Player == player
            && container
            && container.GetInventory() == target;
        QuickStackBulkScope scope = new QuickStackBulkScope(
            player,
            target,
            accountsForPutAway ? operation : null,
            accountsForPutAway ? container : null,
            QuickStackBulkScope.Active);
        if (scope.Operation != null)
        {
            foreach (ItemDrop.ItemData item in source.GetAllItemsInGridOrder())
            {
                if (item != null && item.m_stack > 0)
                {
                    scope.Items.Add(new QuickStackItemSnapshot(item, item.m_stack));
                }
            }
        }

        QuickStackBulkScope.Active = scope;
        return scope;
    }

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
        if (scope.Operation == null
            || activeOperation != scope.Operation
            || scope.Operation?.CurrentContainer != scope.Container)
        {
            return;
        }

        QuickStackOperation operation = scope.Operation!;
        Container container = scope.Container!;
        ResponseGuard.CompleteCurrentResponse(container);
        operation.CurrentContainer = null;
        int movedItems = RecordNativeTransfer(scope, operation, container);
        operation.MovedItems += movedItems;
        Diagnostics.Event("Inventory", "quick_stack_container_result", $"container=\"{container.gameObject.name}\" status=granted moved={movedItems}");
        RequestNextContainer();
    }

    internal static System.Exception? FinalizeBulkStack(QuickStackBulkScope? scope, System.Exception? exception)
    {
        RestoreBulkScope(scope);
        if (exception != null && scope?.Operation != null && activeOperation == scope.Operation)
        {
            if (scope.Container)
            {
                ResponseGuard.CompleteCurrentResponse(scope.Container);
            }

            activeOperation = null;
            Diagnostics.Event("Inventory", "quick_stack_cancelled", "reason=bulk_stack_exception");
        }

        return exception;
    }

    internal static void ResetState()
    {
        activeOperation = null;
        QuickStackBulkScope.Active = null;
        ResponseGuard.Reset();
    }

    internal static void Update()
    {
        ResponseGuard.PruneTimedOutResponses(container => !container);

        QuickStackOperation? operation = activeOperation;
        if (operation == null || !ResponseGuard.TryTimeoutRequest(Time.unscaledTime, out Container? container))
        {
            return;
        }

        if (operation.CurrentContainer != container)
        {
            // Requests are serial; a mismatch must not attach a callback to this batch.
            activeOperation = null;
            QuickStackBulkScope.Active = null;
            Diagnostics.Event("Inventory", "quick_stack_cancelled", "reason=response_guard_mismatch");
            TopLeftFeedbackHud.ShowTransient("Put Away timed out; try again");
            return;
        }

        activeOperation = null;
        QuickStackBulkScope.Active = null;
        string containerName = container != null && container ? container.gameObject.name : "destroyed";
        Diagnostics.Event(
            "Inventory",
            "quick_stack_cancelled",
            $"reason=response_timeout container=\"{containerName}\" wait_seconds={QuickStackResponseGuard<Container>.WaitSeconds:0.#}");
        TopLeftFeedbackHud.ShowTransient("Put Away timed out; try again");
    }

    internal static bool TryHandleTimedOutResponse(Container container, bool granted)
    {
        if (!ResponseGuard.TryDiscardTimedOutResponse(container))
        {
            return false;
        }

        LogDiscardedTimedOutResponse(container, granted);
        return true;
    }

    private static void LogDiscardedTimedOutResponse(Container container, bool granted)
    {
        Diagnostics.Event("Inventory", "quick_stack_late_response_discarded", $"container=\"{container.gameObject.name}\" status={(granted ? "granted" : "denied")}");
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
        return QuickStackBulkScope.Active?.Operation != null
            && type == MessageHud.MessageType.Center
            && message.StartsWith("$msg_stackall");
    }

    private static int RecordNativeTransfer(QuickStackBulkScope scope, QuickStackOperation operation, Container container)
    {
        int movedItems = 0;
        foreach (QuickStackItemSnapshot snapshot in scope.Items)
        {
            int remaining = scope.Player.GetInventory().ContainsItem(snapshot.Item) ? snapshot.Item.m_stack : 0;
            int moved = snapshot.StackBefore - remaining;
            if (moved <= 0)
            {
                continue;
            }

            movedItems += moved;
            string location = QuickStackLocation.Format(operation.Player, container);
            operation.Summary.Add(container.GetInstanceID(), Localize(container.GetHoverName()), location, Localize(snapshot.Item.m_shared.m_name), moved);
            QuickStackDiagnostics.ItemMoved(snapshot.Item, moved, container, location);
        }

        return movedItems;
    }

    private static void Finish(QuickStackOperation operation)
    {
        activeOperation = null;
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
