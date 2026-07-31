using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace BenheimQoL.Portals;

internal sealed class PortalTagHistory
{
    private readonly string path = Path.Combine(Paths.ConfigPath, "BenheimQoL.portal-tags.txt");
    private readonly HashSet<string> tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private bool loaded;

    internal IReadOnlyCollection<string> GetTags()
    {
        EnsureLoaded();
        return tags;
    }

    internal void Remember(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        EnsureLoaded();
        if (!tags.Add(tag.Trim()))
        {
            return;
        }

        Save();
    }

    private void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        if (!File.Exists(path))
        {
            return;
        }

        foreach (string line in File.ReadAllLines(path))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                tags.Add(line.Trim());
            }
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllLines(path, tags);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Could not save portal tag history: {ex.Message}");
        }
    }
}
