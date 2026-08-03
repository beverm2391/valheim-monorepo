using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStack
{
    internal const float Radius = 30f;
    private const float ResponseTimeoutSeconds = 5f;

    private static QuickStackOperation? activeOperation;
    private static readonly HashSet<Container> PendingResponses = new HashSet<Container>();
    private static Container? issuingContainer;

    internal static void Update()
    {
        QuickStackOperation? operation = activeOperation;
        if (operation == null || ReferenceEquals(operation.CurrentContainer, null))
        {
            return;
        }

        if (Time.realtimeSinceStartup < operation.RequestStartedAt + ResponseTimeoutSeconds)
        {
            return;
        }

        Container timedOutContainer = operation.CurrentContainer;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_response_timeout",
            $"container=\"{(timedOutContainer ? timedOutContainer.gameObject.name : "destroyed")}\" timeout_seconds={ResponseTimeoutSeconds:0.#}");
        operation.BusyContainers++;
        operation.CurrentContainer = null;
        RequestNextContainer();
    }

    internal static bool CanSendStackRequest(Container container)
    {
        if (ReferenceEquals(issuingContainer, container) || !PendingResponses.Contains(container))
        {
            return true;
        }

        Diagnostics.Event(
            "Inventory",
            "stack_request_blocked",
            $"container=\"{container.gameObject.name}\" reason=previous_response_pending");
        Player.m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "That chest is still responding");
        return false;
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
            player.Message(MessageHud.MessageType.TopLeft, "Quick stack already in progress");
            return;
        }

        List<Container> containers = NearbyContainerIndex.FindAccessibleContainers(player, Radius, currentContainer);
        Diagnostics.Event("Inventory", "quick_stack_scan", $"containers={containers.Count}");
        if (containers.Count == 0)
        {
            Diagnostics.Event("Inventory", "quick_stack_finished", "moved=0 reason=no_nearby_containers");
            QuickStackFeedback.ShowDetailedResult(player, inventoryWasOpen, "No nearby containers");
            QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(
                player,
                inventoryWasOpen,
                movedItems: 0);
            return;
        }

        QuickStackEligibility eligibility = FindEligibleContainers(player, containers);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_eligibility",
            $"eligible_containers={eligibility.Containers.Count} pocketed={eligibility.SkippedPocketed} no_match={eligibility.SkippedNoMatchingContainer} full={eligibility.SkippedFull}");
        if (eligibility.Containers.Count == 0)
        {
            Diagnostics.Event("Inventory", "quick_stack_finished", "moved=0 reason=no_eligible_containers");
            QuickStackFeedback.ShowDetailedResult(
                player,
                inventoryWasOpen,
                QuickStackMessages.NothingMoved(
                    containers.Count,
                    eligibility.SkippedNoMatchingContainer,
                    eligibility.SkippedFull,
                    skippedBusy: 0));
            QuickStackFeedback.ShowAbovePlayerSummaryIfInventoryWasClosed(
                player,
                inventoryWasOpen,
                movedItems: 0);
            return;
        }

        activeOperation = new QuickStackOperation(
            player,
            inventoryGui,
            eligibility.Containers,
            inventoryWasOpen);
        RequestNextContainer();
    }

    internal static bool TryHandleStackResponse(Container container, bool granted)
    {
        QuickStackOperation? operation = activeOperation;
        if (!PendingResponses.Contains(container))
        {
            return false;
        }

        if (operation == null || operation.CurrentContainer != container)
        {
            PendingResponses.Remove(container);
            Diagnostics.Event(
                "Inventory",
                "quick_stack_stale_response_suppressed",
                $"container=\"{container.gameObject.name}\" granted={Diagnostics.Bool(granted)}");
            return true;
        }

        PendingResponses.Remove(container);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_response",
            $"container=\"{container.gameObject.name}\" granted={Diagnostics.Bool(granted)}");
        if (granted && QuickStackContainerWrite.TryBegin(container, out QuickStackContainerWrite? write))
        {
            int movedItems = MoveEligibleItems(
                operation.Player,
                container,
                container.GetInventory(),
                operation.Summary);
            operation.MovedItems += movedItems;
            write!.Complete(movedItems);
        }
        else
        {
            operation.BusyContainers++;
        }

        operation.CurrentContainer = null;
        RequestNextContainer();
        return true;
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

            if (PendingResponses.Contains(container))
            {
                operation.BusyContainers++;
                Diagnostics.Event(
                    "Inventory",
                    "quick_stack_container_skipped",
                    $"container=\"{container.gameObject.name}\" reason=response_still_pending");
                continue;
            }

            operation.CurrentContainer = container;
            operation.RequestStartedAt = Time.realtimeSinceStartup;
            PendingResponses.Add(container);
            Diagnostics.Event(
                "Inventory",
                "quick_stack_request_container",
                $"container=\"{container.gameObject.name}\" index={operation.NextContainerIndex}/{operation.Containers.Count}");
            try
            {
                issuingContainer = container;
                container.StackAll();
            }
            catch (System.Exception ex)
            {
                PendingResponses.Remove(container);
                operation.CurrentContainer = null;
                operation.BusyContainers++;
                Plugin.Log.LogWarning($"Quick stack request failed for {container.gameObject.name}: {ex.Message}");
                continue;
            }
            finally
            {
                issuingContainer = null;
            }

            return;
        }

        Finish(operation);
    }

    private static int MoveEligibleItems(
        Player player,
        Container container,
        Inventory targetInventory,
        QuickStackSummary summary)
    {
        Inventory playerInventory = player.GetInventory();
        string containerDisplayName = Localize(container.GetHoverName());
        string containerLocation = QuickStackLocation.Format(player, container);
        int movedItems = 0;
        foreach (ItemDrop.ItemData item in new List<ItemDrop.ItemData>(playerInventory.GetAllItemsInGridOrder()))
        {
            if (item == null
                || item.m_stack <= 0
                || PocketItems.IsPocketed(player, item)
                || !targetInventory.ContainsItemByName(item.m_shared.m_name)
                || !targetInventory.CanAddItem(item, 1))
            {
                continue;
            }

            int moved = QuickStackItemTransfer.MoveAsMuchAsPossible(
                playerInventory,
                targetInventory,
                item);
            movedItems += moved;
            summary.Add(
                container.GetInstanceID(),
                containerDisplayName,
                containerLocation,
                Localize(item.m_shared.m_name),
                moved);

            QuickStackDiagnostics.ItemMoved(item, moved, container, containerLocation);
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
            operation.InventoryGui.m_moveItemEffects.Create(operation.InventoryGui.transform.position, Quaternion.identity);
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

    private static string Localize(string name)
    {
        return Localization.instance != null
            ? Localization.instance.Localize(name)
            : name.TrimStart('$');
    }

}
