using System.Collections.Generic;
using BenheimInventoryProtocol;

namespace BenheimQoL.InventoryFeature;

// Selects candidates from local chest observations. Only the owner-authoritative
// transaction protocol may reserve items or mutate a chest.
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
            Inventory? target = container ? container.GetInventory() : null;
            if (target == null)
            {
                continue;
            }

            HashSet<string> targetItemNames = new HashSet<string>();
            foreach (ItemDrop.ItemData item in target.GetAllItems())
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
                // Container.Awake can leave m_inventory null when its ZNetView
                // has no ZDO, including an active chest placement preview. A
                // live Unity component is not inventory readiness.
                Inventory? target = container ? container.GetInventory() : null;
                if (target == null || !target.ContainsItemByName(item.m_shared.m_name))
                {
                    continue;
                }

                foundMatch = true;
                if (!target.CanAddItem(item, 1))
                {
                    continue;
                }

                foundRoom = true;
                seen.Add(container!);
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
        List<DepositCandidate> candidates = new List<DepositCandidate>();
        Inventory? target = container ? container.GetInventory() : null;
        if (target == null)
        {
            return candidates;
        }

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
