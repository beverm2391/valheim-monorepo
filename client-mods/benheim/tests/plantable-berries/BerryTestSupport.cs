using System;
using UnityEngine;

internal static class BerryTestSupport
{
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    internal static void RequireNear(float actual, float expected, string message)
    {
        if (Math.Abs(actual - expected) > 0.0001f)
        {
            throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
        }
    }

    internal static GameObject CreateBerryItem(string name)
    {
        var item = new GameObject(name);
        item.AddComponent<ItemDrop>().m_itemData.Icon = new Sprite();
        return item;
    }

    internal static GameObject CreateBush(string name, GameObject berryItem)
    {
        var bush = new GameObject(name);
        bush.AddComponent<ZNetView>();
        bush.AddComponent<Destructible>();
        Pickable pickable = bush.AddComponent<Pickable>();
        pickable.m_itemPrefab = berryItem;
        pickable.m_respawnTimeMinutes = 300f;
        return bush;
    }
}
