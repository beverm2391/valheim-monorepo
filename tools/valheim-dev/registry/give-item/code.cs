using System;
using UnityEngine;

public static class ValheimDevCommand
{
    [Serializable]
    public sealed class Input
    {
        public string prefab;
        public int amount;
    }

    [Serializable]
    public sealed class Result
    {
        public string prefab;
        public int requested;
        public int added;
        public int before;
        public int after;
        public bool complete;
    }

    public static string Run(string inputJson)
    {
        Input input = JsonUtility.FromJson<Input>(inputJson);
        if (string.IsNullOrWhiteSpace(input.prefab))
            throw new ArgumentException("prefab is required.");
        if (input.amount <= 0)
            throw new ArgumentOutOfRangeException("amount", "amount must be greater than zero.");

        Player player = Player.m_localPlayer;
        if (!player)
            throw new InvalidOperationException("The local player is unavailable.");

        GameObject prefab = ObjectDB.instance.GetItemPrefab(input.prefab);
        if (!prefab)
            throw new ArgumentException("Unknown item prefab: " + input.prefab);

        ItemDrop itemDrop = prefab.GetComponent<ItemDrop>();
        if (!itemDrop)
            throw new InvalidOperationException(input.prefab + " is not an item prefab.");

        Inventory inventory = player.GetInventory();
        string sharedName = itemDrop.m_itemData.m_shared.m_name;
        int before = inventory.CountItems(sharedName);
        int remaining = input.amount;
        int added = 0;
        int maxStack = Math.Max(1, itemDrop.m_itemData.m_shared.m_maxStackSize);

        while (remaining > 0)
        {
            int chunk = Math.Min(maxStack, remaining);
            if (!inventory.CanAddItem(prefab, chunk) || !inventory.AddItem(prefab, chunk))
                break;
            added += chunk;
            remaining -= chunk;
        }

        return JsonUtility.ToJson(new Result {
            prefab = input.prefab,
            requested = input.amount,
            added = added,
            before = before,
            after = inventory.CountItems(sharedName),
            complete = added == input.amount
        });
    }
}
