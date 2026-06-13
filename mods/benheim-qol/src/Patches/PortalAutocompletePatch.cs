using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Patches;

[HarmonyPatch(typeof(TextInput), "Update")]
internal static class PortalAutocompletePatch
{
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
        foreach (TeleportWorld portal in UnityEngine.Object.FindObjectsByType<TeleportWorld>(FindObjectsSortMode.None))
        {
            string tag = portal.GetText();
            if (string.IsNullOrWhiteSpace(tag) || !seen.Add(tag))
            {
                continue;
            }

            tags.Add(tag);
        }

        tags.Sort(StringComparer.OrdinalIgnoreCase);
        return tags;
    }
}
