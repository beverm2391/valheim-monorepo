using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackItemTransfer
{
    internal static int MoveAsMuchAsPossible(
        Inventory sourceInventory,
        Inventory targetInventory,
        ItemDrop.ItemData item)
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
}
