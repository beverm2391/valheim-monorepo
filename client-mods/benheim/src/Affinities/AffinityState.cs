using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Affinities;

internal enum AffinityLoadResult
{
    None,
    Lunge,
    Snipe,
    Test,
    Unsupported,
}

internal static class AffinityState
{
    internal const string CustomDataKey = "com.benheim.qol:affinity";
    internal const string LungeValue = "v1:lunge";
    internal const string SnipeValue = "v1:snipe";
    internal const string TestValue = "v1:test";
    internal const string ClubPrefab = "Club";
    internal const string SnipeBowPrefab = "BowHuntsman";

    internal static bool IsEligibleClub(ItemDrop.ItemData? item)
    {
        return IsEligibleFor(item, ClubPrefab, AffinityLoadResult.Lunge);
    }

    internal static bool IsEligibleSnipeBow(ItemDrop.ItemData? item)
    {
        return IsEligibleFor(item, SnipeBowPrefab, AffinityLoadResult.Snipe);
    }

    internal static bool IsLunge(ItemDrop.ItemData? item)
    {
        return IsSupportedWeapon(item, ClubPrefab) && Read(item) == AffinityLoadResult.Lunge;
    }

    internal static bool IsSnipe(ItemDrop.ItemData? item)
    {
        return IsSupportedWeapon(item, SnipeBowPrefab) && Read(item) == AffinityLoadResult.Snipe;
    }

    internal static bool IsEligibleFor(ItemDrop.ItemData? item, AffinityCatalogEntry entry)
    {
        return IsEligibleFor(item, entry.WeaponPrefab, entry.Affinity);
    }

    internal static bool SupportsAffinity(ItemDrop.ItemData? item, AffinityLoadResult affinity)
    {
        if (affinity == AffinityLoadResult.Test)
        {
            return IsSupportedWeapon(item, ClubPrefab)
                || IsSupportedWeapon(item, SnipeBowPrefab);
        }
        if (affinity == AffinityLoadResult.Lunge) return IsSupportedWeapon(item, ClubPrefab);
        if (affinity == AffinityLoadResult.Snipe) return IsSupportedWeapon(item, SnipeBowPrefab);
        return false;
    }

    internal static bool IsEligibleForAffinity(ItemDrop.ItemData? item, AffinityLoadResult affinity)
    {
        if (affinity == AffinityLoadResult.Test) return SupportsAffinity(item, affinity);
        if (affinity == AffinityLoadResult.Lunge) return IsEligibleClub(item);
        if (affinity == AffinityLoadResult.Snipe) return IsEligibleSnipeBow(item);
        return false;
    }

    internal static bool IsCanonicalPrefab(ItemDrop.ItemData? item, string prefabName)
    {
        GameObject? canonicalPrefab = ObjectDB.instance?.GetItemPrefab(prefabName);
        return item?.m_dropPrefab != null
            && canonicalPrefab != null
            && ReferenceEquals(item.m_dropPrefab, canonicalPrefab);
    }

    internal static bool IsSupportedWeapon(ItemDrop.ItemData? item, string prefabName)
    {
        return item != null
            && AffinityRules.IsNativeWeapon(
                IsCanonicalPrefab(item, prefabName),
                item.m_quality,
                item.m_shared.m_maxQuality);
    }

    private static bool IsEligibleFor(
        ItemDrop.ItemData? item,
        string prefabName,
        AffinityLoadResult affinity)
    {
        return item != null
            && AffinityRules.IsEligibleWeapon(
                IsCanonicalPrefab(item, prefabName),
                item.m_quality,
                item.m_shared.m_maxQuality,
                affinity);
    }

    internal static AffinityLoadResult Read(ItemDrop.ItemData? item)
    {
        return AffinityRules.ReadStoredValue(StoredValue(item));
    }

    internal static string StoredValue(ItemDrop.ItemData? item)
    {
        string? stored = null;
        item?.m_customData?.TryGetValue(CustomDataKey, out stored);
        return stored ?? string.Empty;
    }

    internal static AffinityLoadResult Load(ItemDrop.ItemData? item, string source)
    {
        string stored = StoredValue(item);
        AffinityLoadResult result = Read(item);

        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_state_loaded")
                .String("source", source)
                .String("result", result.ToString().ToLowerInvariant())
                .String("stored_value", stored)
                .String("item_prefab", ItemPrefab(item)));
        return result;
    }

    internal static void Write(ItemDrop.ItemData item, AffinityLoadResult affinity, string source, bool replacing)
    {
        string value = affinity switch
        {
            AffinityLoadResult.Lunge => LungeValue,
            AffinityLoadResult.Snipe => SnipeValue,
            AffinityLoadResult.Test => TestValue,
            _ => throw new ArgumentOutOfRangeException(nameof(affinity)),
        };
        item.m_customData ??= new Dictionary<string, string>();
        item.m_customData[CustomDataKey] = value;
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_state_written")
                .String("source", source)
                .String("affinity", affinity.ToString().ToLowerInvariant())
                .Integer("version", 1)
                .Boolean("replacing", replacing)
                .String("item_prefab", ItemPrefab(item)));
    }

    internal static bool Clear(ItemDrop.ItemData item, string source)
    {
        bool removed = item.m_customData != null && item.m_customData.Remove(CustomDataKey);
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_state_cleared")
                .String("source", source)
                .Boolean("removed", removed)
                .String("item_prefab", ItemPrefab(item)));
        return removed;
    }

    internal static string ItemPrefab(ItemDrop.ItemData? item)
    {
        return item?.m_dropPrefab != null ? item.m_dropPrefab.name : string.Empty;
    }
}
