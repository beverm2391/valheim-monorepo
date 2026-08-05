using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStack
{
    internal const float Radius = 30f;

    private static QuickStackOperation? activeOperation;

    internal static void Run(Player player, InventoryGui inventoryGui, Container? currentContainer)
    {
        bool inventoryWasOpen = InventoryVisibility.IsOpen(inventoryGui);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_requested",
            $"radius={Radius:0.#} inventory_open={Diagnostics.Bool(inventoryWasOpen)}");
        if (activeOperation != null)
        {
            Diagnostics.Event("Inventory", "quick_stack_rejected", "reason=already_in_progress");
            player.Message(MessageHud.MessageType.TopLeft, "Put Away already in progress");
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

            int candidates = CountCandidates(operation.Player, container);
            if (candidates == 0)
            {
                continue;
            }

            operation.CurrentContainer = container;
            operation.RequestedContainers.Add(container);
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

    // Container.StackAll performs Valheim's normal access and ownership handshake. The
    // original response continues into Inventory.StackAll; our scoped patches filter only
    // the protected source items and collect the resulting native transfer delta.
    internal static bool BeginNativeStackResponse(Container container, bool granted)
    {
        QuickStackOperation? operation = activeOperation;
        if (operation == null || !operation.RequestedContainers.Contains(container))
        {
            return true;
        }

        // Suppress a duplicate response for this operation rather than allowing Valheim's
        // unfiltered StackAll handler to run after we have already advanced to another chest.
        if (operation.CurrentContainer != container)
        {
            Diagnostics.Event(
                "Inventory",
                "quick_stack_stale_response",
                $"container=\"{container.gameObject.name}\" granted={Diagnostics.Bool(granted)}");
            return false;
        }

        operation.ResponseInProgress = true;
        operation.ResponseGranted = granted;
        operation.ResponseItems.Clear();
        foreach (ItemDrop.ItemData item in operation.Player.GetInventory().GetAllItemsInGridOrder())
        {
            if (item != null && item.m_stack > 0)
            {
                operation.ResponseItems.Add(new QuickStackItemSnapshot(item, item.m_stack));
            }
        }

        Diagnostics.Event(
            "Inventory",
            "quick_stack_response",
            $"container=\"{container.gameObject.name}\" granted={Diagnostics.Bool(granted)}");
        return true;
    }

    internal static void CompleteNativeStackResponse(Container container)
    {
        QuickStackOperation? operation = activeOperation;
        if (operation == null
            || operation.CurrentContainer != container
            || !operation.ResponseInProgress)
        {
            return;
        }

        operation.ResponseInProgress = false;
        operation.CurrentContainer = null;
        string containerDisplayName = Localize(container.GetHoverName());
        string containerLocation = QuickStackLocation.Format(operation.Player, container);
        if (!operation.ResponseGranted)
        {
            operation.BusyContainers++;
            Diagnostics.Event(
                "Inventory",
                "quick_stack_container_result",
                $"container=\"{container.gameObject.name}\" status=denied moved=0");
            RequestNextContainer();
            return;
        }

        int movedItems = RecordNativeTransfer(operation.Player.GetInventory(), container, containerDisplayName, containerLocation, operation);
        operation.MovedItems += movedItems;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_container_result",
            $"container=\"{container.gameObject.name}\" status=granted moved={movedItems}");
        RequestNextContainer();
    }

    // This runs after Valheim's Inventory.StackAll. A partial target fills by reducing the
    // source ItemData stack but does not remove that source item, so the before/after delta
    // is the only reliable way to report the moved count without rewriting native behavior.
    private static int RecordNativeTransfer(
        Inventory source,
        Container container,
        string containerDisplayName,
        string containerLocation,
        QuickStackOperation operation)
    {
        int movedItems = 0;
        foreach (QuickStackItemSnapshot snapshot in operation.ResponseItems)
        {
            ItemDrop.ItemData item = snapshot.Item;
            int remaining = source.ContainsItem(item) ? item.m_stack : 0;
            int moved = snapshot.StackBefore - remaining;
            if (moved <= 0)
            {
                continue;
            }

            movedItems += moved;
            operation.Summary.Add(
                container.GetInstanceID(),
                containerDisplayName,
                containerLocation,
                Localize(item.m_shared.m_name),
                moved);
            QuickStackDiagnostics.ItemMoved(item, moved, container, containerLocation);
        }

        return movedItems;
    }

    internal static bool ShouldAllowNativeAdd(Inventory target, ItemDrop.ItemData item)
    {
        QuickStackOperation? operation = activeOperation;
        if (operation == null
            || !operation.ResponseInProgress
            || operation.CurrentContainer == null
            || operation.CurrentContainer.GetInventory() != target
            || !operation.ContainsResponseItem(item)
            || !PocketItems.IsPocketed(operation.Player, item))
        {
            return true;
        }

        Diagnostics.Event(
            "Inventory",
            "quick_stack_item_skipped",
            $"item={item.m_shared.m_name} reason=pocketed");
        return false;
    }

    internal static bool ShouldSuppressNativeStackMessage(MessageHud.MessageType type, string message)
    {
        QuickStackOperation? operation = activeOperation;
        return operation != null
            && operation.ResponseInProgress
            && type == MessageHud.MessageType.Center
            && (message.StartsWith("$msg_stackall") || message == "$msg_inuse");
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
