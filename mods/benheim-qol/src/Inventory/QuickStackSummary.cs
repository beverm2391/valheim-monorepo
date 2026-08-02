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
            summary = new ContainerSummary(containerDisplayName, containerLocation);
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
            lines.Add(
                $"{container.DisplayName} {index + 1} ({container.Location}): {FormatItems(container)}");
        }

        return string.Join("\n", lines);
    }

    internal string FormatItemsForContainer(int containerInstanceId)
    {
        return containersByInstanceId.TryGetValue(containerInstanceId, out ContainerSummary? container)
            ? FormatItems(container)
            : string.Empty;
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
        internal ContainerSummary(string displayName, string location)
        {
            DisplayName = displayName;
            Location = location;
        }

        internal string DisplayName { get; }
        internal string Location { get; }
        internal Dictionary<string, int> MovedByItemName { get; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
