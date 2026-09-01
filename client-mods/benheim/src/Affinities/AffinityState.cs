using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Affinities;

internal enum AffinityLoadResult
{
    None,
    Lunge,
    Unsupported,
}

internal static class AffinityState
{
    internal const string CustomDataKey = "com.benheim.qol:affinity";
    internal const string LungeValue = "v1:lunge";
    internal const string ClubPrefab = "Club";

    internal static bool IsEligibleClub(ItemDrop.ItemData? item)
    {
        GameObject? canonicalClub = ObjectDB.instance?.GetItemPrefab(ClubPrefab);
        return item != null
            && item.m_dropPrefab != null
            && canonicalClub != null
            && AffinityRules.IsEligibleClub(
                ReferenceEquals(item.m_dropPrefab, canonicalClub),
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

    internal static void WriteLunge(ItemDrop.ItemData item, string source, bool replacing)
    {
        item.m_customData ??= new Dictionary<string, string>();
        item.m_customData[CustomDataKey] = LungeValue;
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_state_written")
                .String("source", source)
                .String("affinity", "lunge")
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
