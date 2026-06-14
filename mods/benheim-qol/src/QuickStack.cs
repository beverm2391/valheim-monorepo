using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL;

internal static class QuickStack
{
    private const float Radius = 10f;

    internal static void Run(Player player, InventoryGui inventoryGui, Container? currentContainer)
    {
        Inventory playerInventory = player.GetInventory();
        List<Container> containers = NearbyContainerIndex.FindAccessibleContainers(player, Radius, currentContainer);
        if (containers.Count == 0)
        {
            player.Message(MessageHud.MessageType.TopLeft, "No nearby containers");
            return;
        }

        Dictionary<string, int> movedByName = new Dictionary<string, int>();
        int movedStacks = 0;

        List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(playerInventory.GetAllItemsInGridOrder());
        foreach (ItemDrop.ItemData item in items)
        {
            if (item == null || item.m_stack <= 0 || PocketItems.IsPocketed(player, item))
            {
                continue;
            }

            foreach (Container container in containers)
            {
                if (item.m_stack <= 0)
                {
                    break;
                }

                Inventory targetInventory = container.GetInventory();
                if (!targetInventory.ContainsItemByName(item.m_shared.m_name) || !targetInventory.CanAddItem(item, 1))
                {
                    continue;
                }

                if (!NearbyContainerIndex.TryClaim(container))
                {
                    continue;
                }

                int moved = MoveAsMuchAsPossible(playerInventory, targetInventory, item);
                if (moved <= 0)
                {
                    continue;
                }

                movedStacks++;
                AddMoved(movedByName, PocketItems.GetDisplayName(item), moved);
            }
        }

        if (movedStacks == 0)
        {
            player.Message(MessageHud.MessageType.TopLeft, "Nothing to quick stack");
            return;
        }

        inventoryGui.m_moveItemEffects.Create(inventoryGui.transform.position, Quaternion.identity);
        player.Message(MessageHud.MessageType.TopLeft, $"Quick stacked {TotalMoved(movedByName)} items");
    }

    private static int MoveAsMuchAsPossible(Inventory sourceInventory, Inventory targetInventory, ItemDrop.ItemData item)
    {
        int amount = Mathf.Min(item.m_stack, GetCapacityFor(targetInventory, item));
        if (amount <= 0)
        {
            return 0;
        }

        ItemDrop.ItemData clone = item.Clone();
        clone.m_stack = amount;
        targetInventory.AddItem(clone);

        int moved = amount - Mathf.Max(clone.m_stack, 0);
        if (moved <= 0)
        {
            return 0;
        }

        sourceInventory.RemoveItem(item, moved);
        return moved;
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

    private static void AddMoved(Dictionary<string, int> movedByName, string name, int amount)
    {
        if (!movedByName.ContainsKey(name))
        {
            movedByName[name] = 0;
        }

        movedByName[name] += amount;
    }

    private static int TotalMoved(Dictionary<string, int> movedByName)
    {
        int total = 0;
        foreach (int amount in movedByName.Values)
        {
            total += amount;
        }

        return total;
    }
}
