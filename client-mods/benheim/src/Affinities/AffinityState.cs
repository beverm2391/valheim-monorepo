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
    Unsupported,
}

internal static class AffinityState
{
    internal const string CustomDataKey = "com.benheim.qol:affinity";
    internal const string LungeValue = "v1:lunge";
    internal const string SnipeValue = "v1:snipe";
    internal const string ClubPrefab = "Club";
    internal const string SnipeBowPrefab = "BowHuntsman";

    internal static bool IsEligibleClub(ItemDrop.ItemData? item)
    {
        return IsEligiblePrefab(item, ClubPrefab);
    }

    internal static bool IsEligibleSnipeBow(ItemDrop.ItemData? item)
    {
        return IsEligiblePrefab(item, SnipeBowPrefab);
    }

    internal static bool IsLunge(ItemDrop.ItemData? item)
    {
        return IsEligibleClub(item) && Read(item) == AffinityLoadResult.Lunge;
    }

    internal static bool IsSnipe(ItemDrop.ItemData? item)
    {
        return IsEligibleSnipeBow(item) && Read(item) == AffinityLoadResult.Snipe;
    }

    // Each supported weapon has one candidate affinity in this slice. Keep
    // the exact prefab boundary here so the Forge and application agree.
    internal static AffinityLoadResult AvailableFor(ItemDrop.ItemData? item)
    {
        if (IsEligibleClub(item)) return AffinityLoadResult.Lunge;
        if (IsEligibleSnipeBow(item)) return AffinityLoadResult.Snipe;
        return AffinityLoadResult.None;
    }

    internal static bool IsCanonicalPrefab(ItemDrop.ItemData? item, string prefabName)
    {
        GameObject? canonicalPrefab = ObjectDB.instance?.GetItemPrefab(prefabName);
        return item?.m_dropPrefab != null
            && canonicalPrefab != null
            && ReferenceEquals(item.m_dropPrefab, canonicalPrefab);
    }

    private static bool IsEligiblePrefab(ItemDrop.ItemData? item, string prefabName)
    {
        return item != null
            && AffinityRules.IsEligibleWeapon(
                IsCanonicalPrefab(item, prefabName),
                item.m_quality,
                item.m_shared.m_maxQuality);
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
