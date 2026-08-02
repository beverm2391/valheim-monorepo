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
        if (inventoryWasOpen)
        {
            player.Message(MessageHud.MessageType.Center, message);
            return;
        }

        QuickStackReceiptHud.Show(message);
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

            WorldFeedback.ShowAbove(container.transform, Vector3.up * 1.5f, items);
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
