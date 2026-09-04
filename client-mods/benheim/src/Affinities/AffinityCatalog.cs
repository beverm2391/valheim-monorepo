using System.Collections.Generic;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.Affinities;

internal readonly struct AffinityCatalogEntry
{
    internal AffinityCatalogEntry(string weaponPrefab, AffinityLoadResult affinity)
    {
        WeaponPrefab = weaponPrefab;
        Affinity = affinity;
    }

    internal string WeaponPrefab { get; }
    internal AffinityLoadResult Affinity { get; }

    internal bool Matches(AffinityCatalogEntry other)
    {
        return WeaponPrefab == other.WeaponPrefab && Affinity == other.Affinity;
    }
}

internal static class AffinityCatalog
{
    private static readonly AffinityCatalogEntry[] Catalog =
    {
        new(AffinityState.ClubPrefab, AffinityLoadResult.Lunge),
        new(AffinityState.ClubPrefab, AffinityLoadResult.Test),
        new(AffinityState.SnipeBowPrefab, AffinityLoadResult.Snipe),
        new(AffinityState.SnipeBowPrefab, AffinityLoadResult.Test),
    };

    internal static IReadOnlyList<AffinityCatalogEntry> All => Catalog;

    internal static void GetUnlocked(Player? player, List<AffinityCatalogEntry> unlocked)
    {
        unlocked.Clear();
        if (player == null) return;

        for (int index = 0; index < Catalog.Length; index++)
        {
            AffinityCatalogEntry entry = Catalog[index];
            ItemDrop? weapon = WeaponDrop(entry);
            ItemDrop? material = AffinityApplication.ResourceDrop(
                AffinityPresentation.RequirementsFor(entry.Affinity));
            if (weapon == null || material == null) continue;

            bool weaponKnown = player.IsRecipeKnown(weapon.m_itemData.m_shared.m_name);
            bool materialKnown = player.IsKnownMaterial(material.m_itemData.m_shared.m_name);
            AffinityDiagnostics.Emit(
                DiagnosticEvent.Create("Affinity", "affinity_catalog_discovery")
                    .String("weapon_prefab", entry.WeaponPrefab)
                    .String("affinity", entry.Affinity.ToString().ToLowerInvariant())
                    .Boolean("weapon_known", weaponKnown)
                    .Boolean("materials_known", materialKnown)
                    .Boolean("unlocked", weaponKnown && materialKnown));
            if (weaponKnown && materialKnown) unlocked.Add(entry);
        }
    }

    internal static void GetOwnedWeapons(
        Player? player,
        AffinityCatalogEntry entry,
        List<ItemDrop.ItemData> owned)
    {
        owned.Clear();
        if (player == null) return;

        List<ItemDrop.ItemData> inventoryItems = player.GetInventory().GetAllItems();
        for (int index = 0; index < inventoryItems.Count; index++)
        {
            ItemDrop.ItemData item = inventoryItems[index];
            if (AffinityState.IsSupportedWeapon(item, entry.WeaponPrefab)) owned.Add(item);
        }
    }

    internal static ItemDrop? WeaponDrop(AffinityCatalogEntry entry)
    {
        return ObjectDB.instance?.GetItemPrefab(entry.WeaponPrefab)?.GetComponent<ItemDrop>();
    }
}
