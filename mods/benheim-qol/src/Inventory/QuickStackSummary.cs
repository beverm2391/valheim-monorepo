using System;
using System.Collections.Generic;
using System.Linq;

namespace BenheimQoL.InventoryFeature;

internal sealed class QuickStackSummary
{
    private const int MaxNamedTypes = 5;

    private readonly Dictionary<string, int> movedByItemName =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    internal void Add(ItemDrop.ItemData item, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        string displayName = GetDisplayName(item);
        movedByItemName.TryGetValue(displayName, out int previous);
        movedByItemName[displayName] = previous + amount;
    }

    internal string Format()
    {
        List<KeyValuePair<string, int>> items = movedByItemName
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
        List<string> parts = items
            .Take(MaxNamedTypes)
            .Select(pair => $"{pair.Value}x {pair.Key}")
            .ToList();
        if (items.Count > MaxNamedTypes)
        {
            parts.Add($"+{items.Count - MaxNamedTypes} more types");
        }

        return "Put away " + string.Join(", ", parts);
    }

    private static string GetDisplayName(ItemDrop.ItemData item)
    {
        string name = item.m_shared.m_name;
        return Localization.instance != null
            ? Localization.instance.Localize(name)
            : name.TrimStart('$');
    }
}
