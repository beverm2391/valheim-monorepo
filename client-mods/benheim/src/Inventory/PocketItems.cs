using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.InventoryFeature;

internal static class PocketItems
{
    private const string InstancePocketKey = "com.benheim.qol:pocketed";
    private const string PocketedValue = "1";

    private static readonly string Path = System.IO.Path.Combine(Paths.ConfigPath, "BenheimQoL.pocket-items.txt");
    private static readonly HashSet<string> ItemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool loaded;

    internal static bool IsPocketed(Player player, ItemDrop.ItemData item)
    {
        return item != null
            && (IsAutomaticallyProtected(player, item)
                || IsManuallyPocketed(item));
    }

    internal static bool IsAutomaticallyProtected(Player player, ItemDrop.ItemData item)
    {
        return item != null
            && (item.m_equipped
                || player.IsItemEquiped(item)
                || IsHotbarItem(item));
    }

    internal static bool IsManuallyPocketed(ItemDrop.ItemData item)
    {
        EnsureLoaded();
        if (item == null)
        {
            return false;
        }

        if (UsesTypeProtection(item))
        {
            return ItemKeys.Contains(GetItemKey(item));
        }

        return item.m_customData != null
            && item.m_customData.TryGetValue(InstancePocketKey, out string value)
            && value == PocketedValue;
    }

    internal static bool Toggle(ItemDrop.ItemData item, out bool pocketed)
    {
        EnsureLoaded();
        if (!UsesTypeProtection(item))
        {
            return ToggleInstance(item, out pocketed);
        }

        string key = GetItemKey(item);
        if (string.IsNullOrWhiteSpace(key))
        {
            pocketed = false;
            return false;
        }

        if (ItemKeys.Contains(key))
        {
            ItemKeys.Remove(key);
            pocketed = false;
        }
        else
        {
            ItemKeys.Add(key);
            pocketed = true;
        }

        Save();
        return true;
    }

    internal static string GetProtectionScope(ItemDrop.ItemData item)
    {
        return UsesTypeProtection(item) ? "item_type" : "item_instance";
    }

    internal static string GetDisplayName(ItemDrop.ItemData item)
    {
        string name = item?.m_shared?.m_name ?? string.Empty;
        return Localization.instance != null ? Localization.instance.Localize(name) : name;
    }

    private static bool IsHotbarItem(ItemDrop.ItemData item)
    {
        return item.m_gridPos.y == 0 && item.m_gridPos.x >= 0 && item.m_gridPos.x < 8;
    }

    private static bool UsesTypeProtection(ItemDrop.ItemData item)
    {
        return item?.m_shared?.m_maxStackSize > 1;
    }

    private static bool ToggleInstance(ItemDrop.ItemData item, out bool pocketed)
    {
        string legacyTypeKey = GetItemKey(item);
        bool removedLegacyType = ItemKeys.Remove(legacyTypeKey);
        item.m_customData ??= new Dictionary<string, string>();

        if (item.m_customData.ContainsKey(InstancePocketKey))
        {
            item.m_customData.Remove(InstancePocketKey);
            pocketed = false;
        }
        else
        {
            item.m_customData[InstancePocketKey] = PocketedValue;
            pocketed = true;
        }

        if (removedLegacyType)
        {
            Save();
        }

        return true;
    }

    private static string GetItemKey(ItemDrop.ItemData item)
    {
        return item.m_dropPrefab ? item.m_dropPrefab.name : item.m_shared?.m_name ?? string.Empty;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        if (!File.Exists(Path))
        {
            Diagnostics.Event("Inventory", "pocket_history_loaded", "items=0 file_exists=false");
            return;
        }

        foreach (string line in File.ReadAllLines(Path))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                ItemKeys.Add(line.Trim());
            }
        }

        Diagnostics.Event("Inventory", "pocket_history_loaded", $"items={ItemKeys.Count} file_exists=true");
    }

    private static void Save()
    {
        try
        {
            File.WriteAllLines(Path, ItemKeys);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not save pocketed item list: {ex.Message}");
        }
    }
}
