using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStack
{
    internal const float Radius = 30f;
    private const float ResponseTimeoutSeconds = 5f;

    private static Operation? activeOperation;
    private static readonly HashSet<Container> PendingResponses = new HashSet<Container>();
    private static Container? issuingContainer;

    internal static void Update()
    {
        Operation? operation = activeOperation;
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
        Diagnostics.Event("Inventory", "quick_stack_requested", $"radius={Radius:0.#}");
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
            player.Message(MessageHud.MessageType.TopLeft, "No nearby containers");
            return;
        }

        Eligibility eligibility = FindEligibleContainers(player, containers);
        Diagnostics.Event(
            "Inventory",
            "quick_stack_eligibility",
            $"eligible_containers={eligibility.Containers.Count} pocketed={eligibility.SkippedPocketed} no_match={eligibility.SkippedNoMatchingContainer} full={eligibility.SkippedFull}");
        if (eligibility.Containers.Count == 0)
        {
            Diagnostics.Event("Inventory", "quick_stack_finished", "moved=0 reason=no_eligible_containers");
            player.Message(
                MessageHud.MessageType.TopLeft,
                QuickStackMessages.NothingMoved(
                    containers.Count,
                    eligibility.SkippedPocketed,
                    eligibility.SkippedNoMatchingContainer,
                    eligibility.SkippedFull,
                    skippedBusy: 0));
            return;
        }

        activeOperation = new Operation(player, inventoryGui, eligibility.Containers);
        RequestNextContainer();
    }

    internal static bool TryHandleStackResponse(Container container, bool granted)
    {
        Operation? operation = activeOperation;
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
        if (granted)
        {
            operation.MovedItems += MoveEligibleItems(
                operation.Player,
                container,
                container.GetInventory(),
                operation.Summary);
        }
        else
        {
            operation.BusyContainers++;
        }

        operation.CurrentContainer = null;
        RequestNextContainer();
        return true;
    }

    private static Eligibility FindEligibleContainers(Player player, List<Container> containers)
    {
        Eligibility eligibility = new Eligibility();
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
        Operation? operation = activeOperation;
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

            int moved = MoveAsMuchAsPossible(playerInventory, targetInventory, item);
            movedItems += moved;
            summary.Add(
                container.GetInstanceID(),
                Localize(container.GetHoverName()),
                Localize(item.m_shared.m_name),
                moved);

            Diagnostics.Event(
                "Inventory",
                "quick_stack_item",
                $"item={item.m_shared.m_name} moved={moved}");
        }

        return movedItems;
    }

    private static void Finish(Operation operation)
    {
        activeOperation = null;
        Diagnostics.Event(
            "Inventory",
            "quick_stack_finished",
            $"moved={operation.MovedItems} busy_containers={operation.BusyContainers}");
        if (operation.MovedItems > 0)
        {
            operation.InventoryGui.m_moveItemEffects.Create(operation.InventoryGui.transform.position, Quaternion.identity);
            operation.Player.Message(
                MessageHud.MessageType.TopLeft,
                operation.Summary.Format());
            return;
        }

        operation.Player.Message(
            MessageHud.MessageType.TopLeft,
            QuickStackMessages.NothingMoved(operation.Containers.Count, 0, 0, 0, operation.BusyContainers));
    }

    private static int MoveAsMuchAsPossible(Inventory sourceInventory, Inventory targetInventory, ItemDrop.ItemData item)
    {
        int amount = Mathf.Min(item.m_stack, GetCapacityFor(targetInventory, item));
        if (amount <= 0)
        {
            return 0;
        }

        int before = CountMatchingItems(targetInventory, item);
        ItemDrop.ItemData clone = item.Clone();
        clone.m_stack = amount;
        targetInventory.AddItem(clone);

        int moved = Mathf.Clamp(CountMatchingItems(targetInventory, item) - before, 0, amount);
        if (moved <= 0)
        {
            return 0;
        }

        sourceInventory.RemoveItem(item, moved);
        return moved;
    }

    private static int CountMatchingItems(Inventory inventory, ItemDrop.ItemData item)
    {
        int count = 0;
        foreach (ItemDrop.ItemData storedItem in inventory.GetAllItems())
        {
            if (storedItem.m_shared.m_name == item.m_shared.m_name
                && storedItem.m_quality == item.m_quality
                && storedItem.m_worldLevel == item.m_worldLevel)
            {
                count += storedItem.m_stack;
            }
        }

        return count;
    }

    private static string Localize(string name)
    {
        return Localization.instance != null
            ? Localization.instance.Localize(name)
            : name.TrimStart('$');
    }

    private static int GetCapacityFor(Inventory inventory, ItemDrop.ItemData item)
    {
        int capacity = 0;
        int occupied = 0;
        foreach (ItemDrop.ItemData storedItem in inventory.GetAllItems())
        {
            occupied++;
            if (storedItem.m_shared.m_name == item.m_shared.m_name
                && storedItem.m_quality == item.m_quality
                && storedItem.m_worldLevel == item.m_worldLevel
                && storedItem.m_stack < storedItem.m_shared.m_maxStackSize)
            {
                capacity += storedItem.m_shared.m_maxStackSize - storedItem.m_stack;
            }
        }

        int emptySlots = inventory.GetWidth() * inventory.GetHeight() - occupied;
        capacity += Mathf.Max(0, emptySlots) * item.m_shared.m_maxStackSize;
        return capacity;
    }

    private sealed class Eligibility
    {
        internal List<Container> Containers { get; } = new List<Container>();
        internal int SkippedPocketed { get; set; }
        internal int SkippedNoMatchingContainer { get; set; }
        internal int SkippedFull { get; set; }
    }

    private sealed class Operation
    {
        internal Operation(Player player, InventoryGui inventoryGui, List<Container> containers)
        {
            Player = player;
            InventoryGui = inventoryGui;
            Containers = containers;
        }

        internal Player Player { get; }
        internal InventoryGui InventoryGui { get; }
        internal List<Container> Containers { get; }
        internal int NextContainerIndex { get; set; }
        internal Container? CurrentContainer { get; set; }
        internal float RequestStartedAt { get; set; }
        internal int MovedItems { get; set; }
        internal int BusyContainers { get; set; }
        internal QuickStackSummary Summary { get; } = new QuickStackSummary();
    }
}
