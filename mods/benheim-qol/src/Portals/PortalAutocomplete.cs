using System;
using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Portals;

internal static class PortalAutocomplete
{
    private static readonly PortalTagHistory TagHistory = new PortalTagHistory();

    private static readonly FieldInfo QueuedTextReceiverField =
        AccessTools.Field(typeof(TextInput), "m_queuedSign");

    private static string lastPrefix = string.Empty;
    private static int lastIndex = -1;

    internal static void CycleMatch(TextInput textInput)
    {
        if (!Input.GetKeyDown(KeyCode.Tab))
        {
            return;
        }

        if (!TextInput.IsVisible())
        {
            Diagnostics.Event("Portals", "autocomplete_rejected", "reason=text_input_hidden");
            return;
        }

        object queuedReceiver = QueuedTextReceiverField.GetValue(textInput);
        if (!(queuedReceiver is TeleportWorld))
        {
            Diagnostics.Event(
                "Portals",
                "autocomplete_rejected",
                $"reason=not_portal receiver={queuedReceiver?.GetType().Name ?? "none"}");
            return;
        }

        List<string> tags = GetKnownPortalTags();
        if (tags.Count == 0)
        {
            Diagnostics.Event("Portals", "autocomplete_rejected", "reason=no_known_tags");
            return;
        }

        string prefix = textInput.m_inputField.text ?? string.Empty;
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
        textInput.m_inputField.text = matches[lastIndex];
        textInput.m_inputField.ActivateInputField();
        textInput.m_inputField.caretPosition = textInput.m_inputField.text.Length;
        textInput.m_inputField.selectionAnchorPosition = textInput.m_inputField.text.Length;
        textInput.m_inputField.selectionFocusPosition = textInput.m_inputField.text.Length;
        Diagnostics.Event(
            "Portals",
            "autocomplete_selected",
            $"prefix=\"{prefix}\" known={tags.Count} matches={matches.Count} selected=\"{matches[lastIndex]}\"");
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

    internal static void RememberTag(string tag)
    {
        if (TagHistory.Remember(tag))
        {
            Diagnostics.Event("Portals", "tag_remembered", $"tag=\"{tag.Trim()}\"");
        }
    }
}
