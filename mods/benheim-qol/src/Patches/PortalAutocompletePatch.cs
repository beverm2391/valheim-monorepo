using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Patches;

[HarmonyPatch(typeof(TextInput), "Update")]
internal static class PortalAutocompletePatch
{
    private static readonly PortalTagHistory TagHistory = new PortalTagHistory();

    private static readonly FieldInfo QueuedTextReceiverField =
        AccessTools.Field(typeof(TextInput), "m_queuedSign");

    private static string lastPrefix = string.Empty;
    private static int lastIndex = -1;

    private static void Postfix(TextInput __instance)
    {
        if (!TextInput.IsVisible() || !Input.GetKeyDown(KeyCode.Tab))
        {
            return;
        }

        object queuedReceiver = QueuedTextReceiverField.GetValue(__instance);
        if (!(queuedReceiver is TeleportWorld))
        {
            return;
        }

        List<string> tags = GetKnownPortalTags();
        if (tags.Count == 0)
        {
            return;
        }

        string prefix = __instance.m_inputField.text ?? string.Empty;
        List<string> matches = tags.FindAll(tag => tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (matches.Count == 0)
        {
            matches = tags;
        }

        if (!string.Equals(lastPrefix, prefix, StringComparison.OrdinalIgnoreCase))
        {
            lastPrefix = prefix;
            lastIndex = -1;
        }

        lastIndex = (lastIndex + 1) % matches.Count;
        __instance.m_inputField.text = matches[lastIndex];
        __instance.m_inputField.ActivateInputField();
        __instance.m_inputField.caretPosition = __instance.m_inputField.text.Length;
        __instance.m_inputField.selectionAnchorPosition = __instance.m_inputField.text.Length;
        __instance.m_inputField.selectionFocusPosition = __instance.m_inputField.text.Length;
    }

    private static List<string> GetKnownPortalTags()
    {
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> tags = new List<string>();
        AddTags(TagHistory.GetTags(), seen, tags);
        foreach (TeleportWorld portal in UnityEngine.Object.FindObjectsByType<TeleportWorld>(FindObjectsSortMode.None))
        {
            string tag = portal.GetText();
            AddTag(tag, seen, tags);
        }

        tags.Sort(StringComparer.OrdinalIgnoreCase);
        return tags;
    }

    private static void AddTags(IEnumerable<string> sourceTags, HashSet<string> seen, List<string> tags)
    {
        foreach (string tag in sourceTags)
        {
            AddTag(tag, seen, tags);
        }
    }

    private static void AddTag(string tag, HashSet<string> seen, List<string> tags)
    {
        if (string.IsNullOrWhiteSpace(tag) || !seen.Add(tag.Trim()))
        {
            return;
        }

        tags.Add(tag.Trim());
    }

    [HarmonyPatch(typeof(TeleportWorld), "GetText")]
    private static class RememberReadPortalTagPatch
    {
        private static void Postfix(string __result)
        {
            TagHistory.Remember(__result);
        }
    }

    [HarmonyPatch(typeof(TeleportWorld), "SetText")]
    private static class RememberWrittenPortalTagPatch
    {
        private static void Prefix(string text)
        {
            TagHistory.Remember(text);
        }
    }

    private sealed class PortalTagHistory
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
}
