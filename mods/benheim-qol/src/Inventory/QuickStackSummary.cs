using System;
using System.Collections.Generic;
using System.Linq;

namespace BenheimQoL.InventoryFeature;

internal sealed class QuickStackSummary
{
    private readonly List<ContainerSummary> containers = new List<ContainerSummary>();
    private readonly Dictionary<int, ContainerSummary> containersByInstanceId =
        new Dictionary<int, ContainerSummary>();

    internal void Add(
        int containerInstanceId,
        string containerDisplayName,
        string itemDisplayName,
        int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!containersByInstanceId.TryGetValue(containerInstanceId, out ContainerSummary? summary))
        {
            summary = new ContainerSummary(containerDisplayName);
            containersByInstanceId.Add(containerInstanceId, summary);
            containers.Add(summary);
        }

        summary.MovedByItemName.TryGetValue(itemDisplayName, out int previous);
        summary.MovedByItemName[itemDisplayName] = previous + amount;
    }

    internal string Format()
    {
        var lines = new List<string>(containers.Count);
        for (int index = 0; index < containers.Count; index++)
        {
            ContainerSummary container = containers[index];
            List<string> parts = container.MovedByItemName
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => $"{pair.Value}x {pair.Key}")
                .ToList();
            lines.Add($"{container.DisplayName} {index + 1}: {string.Join(", ", parts)}");
        }

        return string.Join("\n", lines);
    }

    private sealed class ContainerSummary
    {
        internal ContainerSummary(string displayName)
        {
            DisplayName = displayName;
        }

        internal string DisplayName { get; }
        internal Dictionary<string, int> MovedByItemName { get; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
