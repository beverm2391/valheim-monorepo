using System.Collections.Generic;
using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStack
{
    internal const float Radius = 30f;

    private static QuickStackOperation? activeOperation;

    internal static void Update()
    {
        // Network retries are owned by InventoryTransactions.Update().
    }

    internal static void Run(Player player, InventoryGui inventoryGui, Container? currentContainer)
    {
        bool inventoryWasOpen = InventoryVisibility.IsOpen(inventoryGui);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_requested",
            $"radius={Radius:0.#} inventory_open={Diagnostics.Bool(inventoryWasOpen)}");
        if (!QuickStackAvailability.CanRun(player, inventoryWasOpen))
        {
            return;
        }

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
                if (seen.Add(container))
                {
                    eligibility.Containers.Add(container);
                }
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

            List<DepositCandidate> candidates = FindCandidates(operation.Player, container);
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

    private static List<DepositCandidate> FindCandidates(Player player, Container container)
    {
        Inventory target = container.GetInventory();
        List<DepositCandidate> candidates = new List<DepositCandidate>();
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItemsInGridOrder())
        {
            if (item != null
                && item.m_stack > 0
                && !PocketItems.IsPocketed(player, item)
                && target.ContainsItemByName(item.m_shared.m_name)
                && target.CanAddItem(item, 1))
            {
                candidates.Add(new DepositCandidate(item));
            }
        }

        return candidates;
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
            QuickStackDiagnostics.ItemMoved(entry.Item, entry.Accepted, container, containerLocation);
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
        operation.CurrentContainer = null;
        RequestNextContainer();
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
