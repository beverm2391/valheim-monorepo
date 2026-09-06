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
    internal const string DeveloperBypassKey = "com.benheim.qol:affinity-development-bypass";
    internal const string DeveloperBypassValue = "v1";
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
        return IsActive(item, AffinityLoadResult.Lunge);
    }

    internal static bool IsSnipe(ItemDrop.ItemData? item)
    {
        return IsActive(item, AffinityLoadResult.Snipe);
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

    internal static bool IsActive(ItemDrop.ItemData? item, AffinityLoadResult affinity)
    {
        // Ordinary items must still satisfy the progression gate. Only an item
        // Benheim initialized as a development fixture may carry that bypass
        // through later attacks, equipment changes, and native persistence.
        return SupportsAffinity(item, affinity)
            && Read(item) == affinity
            && (IsEligibleForAffinity(item, affinity) || HasDeveloperBypass(item));
    }

    internal static bool HasDeveloperBypass(ItemDrop.ItemData? item)
    {
        return item?.m_customData != null
            && item.m_customData.TryGetValue(DeveloperBypassKey, out string? stored)
            && string.Equals(stored, DeveloperBypassValue, StringComparison.Ordinal);
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
                .Boolean("developer_bypass", HasDeveloperBypass(item))
                .String("item_prefab", ItemPrefab(item)));
        return result;
    }

    internal static void Write(
        ItemDrop.ItemData item,
        AffinityLoadResult affinity,
        string source,
        bool replacing,
        bool developerBypass = false)
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
        if (developerBypass)
        {
            item.m_customData[DeveloperBypassKey] = DeveloperBypassValue;
        }
        else
        {
            item.m_customData.Remove(DeveloperBypassKey);
        }
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_state_written")
                .String("source", source)
                .String("affinity", affinity.ToString().ToLowerInvariant())
                .Integer("version", 1)
                .Boolean("replacing", replacing)
                .Boolean("developer_bypass", developerBypass)
                .String("item_prefab", ItemPrefab(item)));
    }

    internal static bool Clear(ItemDrop.ItemData item, string source)
    {
        bool removed = item.m_customData != null && item.m_customData.Remove(CustomDataKey);
        bool bypassRemoved = item.m_customData != null && item.m_customData.Remove(DeveloperBypassKey);
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_state_cleared")
                .String("source", source)
                .Boolean("removed", removed)
                .Boolean("developer_bypass_removed", bypassRemoved)
                .String("item_prefab", ItemPrefab(item)));
        return removed;
    }

    internal static string ItemPrefab(ItemDrop.ItemData? item)
    {
        return item?.m_dropPrefab != null ? item.m_dropPrefab.name : string.Empty;
    }
}
