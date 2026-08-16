using System.Collections.Generic;

namespace BenheimQoL.InventoryFeature;

// Owns Put Away's item/chest selection and the accounting around Valheim's
// native StackAll mutation. QuickStack remains the request/response lifecycle.
internal static class QuickStackTransfer
{
    internal static QuickStackEligibility FindEligibleContainers(Player player, List<Container> containers)
    {
        QuickStackEligibility eligibility = new QuickStackEligibility();
        HashSet<Container> seen = new HashSet<Container>();
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItemsInGridOrder())
        {
            if (item == null || item.m_stack <= 0)
            {
                continue;
            }

            if (PocketItems.IsPocketed(player, item))
            {
                eligibility.SkippedPocketed++;
                continue;
            }

            bool foundMatch = false;
            bool foundRoom = false;
            foreach (Container container in containers)
            {
                Inventory target = container.GetInventory();
                if (!target.ContainsItemByName(item.m_shared.m_name))
                {
                    continue;
                }

                foundMatch = true;
                if (!target.CanAddItem(item, 1))
                {
                    continue;
                }

                foundRoom = true;
                seen.Add(container);
            }

            if (!foundMatch)
            {
                eligibility.SkippedNoMatchingContainer++;
            }
            else if (!foundRoom)
            {
                eligibility.SkippedFull++;
            }
        }

        // NearbyContainerIndex already ordered this list nearest-first.
        foreach (Container container in containers)
        {
            if (seen.Contains(container))
            {
                eligibility.Containers.Add(container);
            }
        }

        return eligibility;
    }

    internal static int CountCandidates(Player player, Container container)
    {
        Inventory target = container.GetInventory();
        int candidates = 0;
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItemsInGridOrder())
        {
            if (item != null
                && item.m_stack > 0
                && !PocketItems.IsPocketed(player, item)
                && target.ContainsItemByName(item.m_shared.m_name)
                && target.CanAddItem(item, 1))
            {
                candidates++;
            }
        }

        return candidates;
    }

    internal static int RecordNativeTransfer(
        QuickStackBulkScope scope,
        QuickStackOperation operation,
        Container container)
    {
        int movedItems = 0;
        foreach (QuickStackItemSnapshot snapshot in scope.Items)
        {
            int remaining = scope.Player.GetInventory().ContainsItem(snapshot.Item) ? snapshot.Item.m_stack : 0;
            int moved = snapshot.StackBefore - remaining;
            if (moved <= 0)
            {
                continue;
            }

            movedItems += moved;
            string location = QuickStackLocation.Format(operation.Player, container);
            operation.Summary.Add(
                container.GetInstanceID(),
                Localize(container.GetHoverName()),
                location,
                Localize(snapshot.Item.m_shared.m_name),
                moved);
            QuickStackDiagnostics.ItemMoved(
                operation.OperationId,
                snapshot.Item,
                moved,
                container.GetInventory().CountItems(snapshot.Item.m_shared.m_name),
                container,
                location);
        }

        return movedItems;
    }

    private static string Localize(string name)
    {
        return Localization.instance != null ? Localization.instance.Localize(name) : name.TrimStart('$');
    }
}
