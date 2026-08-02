using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackFeedback
{
    internal static void ShowDetailedResult(
        Player player,
        bool inventoryWasOpen,
        string message)
    {
        MessageHud.MessageType messageType = inventoryWasOpen
            ? MessageHud.MessageType.Center
            : MessageHud.MessageType.TopLeft;
        player.Message(messageType, message);
    }

    internal static void ShowDestinationSummaries(
        IEnumerable<Container> containers,
        QuickStackSummary summary)
    {
        foreach (Container container in containers)
        {
            if (!container)
            {
                continue;
            }

            string items = summary.FormatItemsForContainer(container.GetInstanceID());
            if (string.IsNullOrEmpty(items))
            {
                continue;
            }

            WorldFeedback.ShowAt(container.transform.position + Vector3.up * 1.5f, items);
        }
    }

    internal static void ShowAbovePlayerSummaryIfInventoryWasClosed(
        Player player,
        bool inventoryWasOpen,
        int movedItems)
    {
        if (inventoryWasOpen)
        {
            return;
        }

        WorldFeedback.ShowAbovePlayer(player, QuickStackMessages.AbovePlayerSummary(movedItems));
    }
}
