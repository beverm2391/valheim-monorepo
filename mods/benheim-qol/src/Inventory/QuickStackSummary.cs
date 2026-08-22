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
        int containerOrder,
        string containerDisplayName,
        string containerLocation,
        string itemDisplayName,
        int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        if (!containersByInstanceId.TryGetValue(containerInstanceId, out ContainerSummary? summary))
        {
            summary = new ContainerSummary(containerOrder, containerDisplayName, containerLocation);
            containersByInstanceId.Add(containerInstanceId, summary);
            containers.Add(summary);
        }

        summary.MovedByItemName.TryGetValue(itemDisplayName, out int previous);
        summary.MovedByItemName[itemDisplayName] = previous + amount;
    }

    internal string Format()
    {
        List<ContainerSummary> orderedContainers = containers
            .OrderBy(container => container.Order)
            .ToList();
        var lines = new List<string>(orderedContainers.Count);
        for (int index = 0; index < orderedContainers.Count; index++)
        {
            ContainerSummary container = orderedContainers[index];
            lines.Add(
                $"{container.DisplayName} {index + 1} ({container.Location}): {FormatItems(container)}");
        }

        return string.Join("\n", lines);
    }

    private static string FormatItems(ContainerSummary container)
    {
        List<string> parts = container.MovedByItemName
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Value}x {pair.Key}")
            .ToList();
        return string.Join(", ", parts);
    }

    private sealed class ContainerSummary
    {
        internal ContainerSummary(int order, string displayName, string location)
        {
            Order = order;
            DisplayName = displayName;
            Location = location;
        }

        internal int Order { get; }
        internal string DisplayName { get; }
        internal string Location { get; }
        internal Dictionary<string, int> MovedByItemName { get; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
