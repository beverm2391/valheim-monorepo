using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BenheimQoL.Infrastructure;

namespace BenheimQoL.InventoryFeature;

internal static class PocketItems
{
    private static readonly string Path = System.IO.Path.Combine(Paths.ConfigPath, "BenheimQoL.pocket-items.txt");
    private static readonly HashSet<string> ItemKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static bool loaded;

    internal static bool IsPocketed(Player player, ItemDrop.ItemData item)
    {
        return item != null
            && (item.m_equipped
                || player.IsItemEquiped(item)
                || IsHotbarItem(item)
                || IsManuallyPocketed(item));
    }

    internal static bool IsManuallyPocketed(ItemDrop.ItemData item)
    {
        EnsureLoaded();
        return item != null && ItemKeys.Contains(GetItemKey(item));
    }

    internal static bool Toggle(ItemDrop.ItemData item, out bool pocketed)
    {
        EnsureLoaded();
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

    internal static string GetDisplayName(ItemDrop.ItemData item)
    {
        string name = item?.m_shared?.m_name ?? string.Empty;
        return Localization.instance != null ? Localization.instance.Localize(name) : name;
    }

    private static bool IsHotbarItem(ItemDrop.ItemData item)
    {
        return item.m_gridPos.y == 0 && item.m_gridPos.x >= 0 && item.m_gridPos.x < 8;
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
