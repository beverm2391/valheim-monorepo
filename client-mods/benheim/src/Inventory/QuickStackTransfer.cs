using System.Collections.Generic;
using BenheimInventoryProtocol;

namespace BenheimQoL.InventoryFeature;

// Owns Put Away's item/chest selection and the accounting around Valheim's
// native StackAll mutation. QuickStack remains the request/response lifecycle.
internal static class QuickStackTransfer
{
    internal static bool HasLaterCandidateDependency(
        IReadOnlyCollection<DepositCandidate> candidates,
        IReadOnlyList<Container> containers,
        int nextContainerIndex)
    {
        HashSet<string> candidateItemNames = new HashSet<string>();
        foreach (DepositCandidate candidate in candidates)
        {
            if (candidate.SourceItem != null)
            {
                candidateItemNames.Add(candidate.SourceItem.m_shared.m_name);
            }
        }

        for (int index = nextContainerIndex; index < containers.Count; index++)
        {
            Container container = containers[index];
            if (!container)
            {
                continue;
            }

            HashSet<string> targetItemNames = new HashSet<string>();
            foreach (ItemDrop.ItemData item in container.GetInventory().GetAllItems())
            {
                if (item != null && item.m_stack > 0)
                {
                    targetItemNames.Add(item.m_shared.m_name);
                }
            }

            if (QuickStackBatchDependencies.HasItemNameOverlap(
                    candidateItemNames,
                    targetItemNames))
            {
                return true;
            }
        }

        return false;
    }

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

    internal static List<DepositCandidate> FindCandidates(Player player, Container container)
    {
        Inventory target = container.GetInventory();
        List<DepositCandidate> candidates = new List<DepositCandidate>();
        foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItemsInGridOrder())
        {
            if (item != null
                && item.m_stack > 0
                && !PocketItems.IsPocketed(player, item)
                && target.ContainsItemByName(item.m_shared.m_name)
                && target.CanAddItem(item, 1))
            {
                candidates.Add(new DepositCandidate(item));
            }
        }

        return candidates;
    }
}
