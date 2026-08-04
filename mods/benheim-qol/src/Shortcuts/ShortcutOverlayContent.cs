using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using BenheimQoL.InventoryFeature;
using BenheimInventoryProtocol;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private static readonly Section[] Sections =
    {
        new(
            "Inventory",
            new Color(1f, 0.82f, 0.28f, 1f),
            new[]
            {
                new Entry("Gold P", "Manual; toggle with P; persists when moved", PocketMarker.ManualColor),
                new Entry("Hover + P", "Toggle manual pocketing for this stack or item"),
                new Entry("Left Alt + click", "Toggle manual pocketing for this stack or item"),
                new Entry("Left Shift + P", $"Put matching items away within {QuickStack.Radius:0.#} m"),
                new Entry("Backspace/Delete", "Reset the split amount to 1"),
                new Entry("Enter", "Confirm a split; move it across an open container"),
            },
            "A gold P means manually pocketed. Stackables protect their item type; gear protects only the marked item. Equipped and hotbar items stay protected without a marker."),
        new(
            "Build & Repair",
            new Color(1f, 0.58f, 0.36f, 1f),
            new[]
            {
                new Entry("Shift + station click", "Repair all eligible gear"),
                new Entry("Left Shift + station input", "Fill its available input or fuel capacity"),
            },
            "Stations, cauldrons, and nearby objects have a longer interaction range."),
        new(
            "Farming",
            new Color(0.48f, 0.88f, 0.45f, 1f),
            new[]
            {
                new Entry("Left Shift + interact", $"Harvest matching targets within {Farming.FarmingSettings.HarvestRadius:0.#} m"),
                new Entry("Left Shift + plant", $"Plant a centered {Farming.FarmingSettings.GridWidth}x{Farming.FarmingSettings.GridLength} grid"),
            },
            "Normal resource, stamina, spacing, and cultivated-ground rules still apply."),
        new(
            "Travel",
            new Color(0.42f, 0.84f, 1f, 1f),
            Array.Empty<Entry>(),
            "Portal transitions finish sooner after the destination is ready."),
        new(
            "Combat & Skills",
            new Color(1f, 0.46f, 0.5f, 1f),
            Array.Empty<Entry>(),
            "Pickaxes skill improves mining damage, crits, and AOE after level 25. " +
            "Wood Cutting unlocks CLEAVE after level 25. " +
            "Perfect defenses show adrenaline gains, and the meter shows decay timing."),
        new(
            "Help",
            new Color(0.74f, 0.7f, 1f, 1f),
            new[]
            {
                new Entry("F7", "Save a diagnostic log to the Desktop"),
                new Entry("F8 / Escape", "Close this panel"),
            },
            "Send the exported Benheim log when reporting a problem."),
    };

    private static bool EnsureBuilt()
    {
        if (root != null)
        {
            return true;
        }

        Canvas? canvas = FindNativeCanvas();
        NativeTemplates? templates = FindNativeTemplates();
        if (canvas == null || templates == null)
        {
            if (!buildFailureLogged)
            {
                buildFailureLogged = true;
                Plugin.Log.LogWarning("Could not build the shortcuts panel because native Valheim UI templates are not ready.");
            }
            return false;
        }

        root = CreateRectObject(RootName, canvas.transform).gameObject;
        root.SetActive(false);
        RectTransform blockerRect = (RectTransform)root.transform;
        Stretch(blockerRect);
        Image blocker = root.AddComponent<Image>();
        blocker.color = Color.clear;
        blocker.raycastTarget = true;

        windowRect = CreateRectObject("Window", blockerRect);
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        Image window = windowRect.gameObject.AddComponent<Image>();
        CopyImageStyle(templates.PanelBackground, window);
        window.raycastTarget = true;

        TMP_Text title = CreateText("Title", windowRect, templates.Text, layoutElement: false);
        RectTransform titleRect = (RectTransform)title.transform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(30f, -72f);
        titleRect.offsetMax = new Vector2(-30f, -18f);
        title.text = $"Benheim v{Plugin.PluginVersion}";
        title.fontSize = 34f;
        title.fontStyle = FontStyles.Bold;
        title.alignment = TextAlignmentOptions.Center;

        ScrollRect scroll = CreateNativeScrollView(windowRect, templates);
        RectTransform scrollRect = (RectTransform)scroll.transform;
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(28f, 72f);
        scrollRect.offsetMax = new Vector2(-28f, -80f);
        contentRect = scroll.content;

        AddSectionHeading("Multiplayer Put Away", new Color(0.56f, 0.86f, 1f, 1f), templates.Text);
        multiplayerStatus = CreateText("MultiplayerStatus", contentRect, templates.Text, layoutElement: true);
        multiplayerStatus.fontSize = 20f;
        multiplayerStatus.color = Color.white;
        multiplayerStatus.richText = false;
        multiplayerStatus.text = "Checking multiplayer compatibility...";
        AddSpacer(8f);

        foreach (Section section in Sections)
        {
            AddSectionHeading(section.Name, section.Accent, templates.Text);
            foreach (Entry entry in section.Entries)
            {
                TMP_Text row = CreateText($"{section.Name}Entry", contentRect, templates.Text, layoutElement: true);
                row.fontSize = 20f;
                row.color = entry.Accent ?? Color.white;
                row.text = $"<b>{entry.Key}</b>    <color=#FFFFFF>{entry.Action}</color>";
            }

            TMP_Text note = CreateText($"{section.Name}Note", contentRect, templates.Text, layoutElement: true);
            note.fontSize = 18f;
            note.color = new Color(0.82f, 0.84f, 0.86f, 1f);
            note.text = section.Note;
            AddSpacer(8f);
        }

        closeButton = CreateNativeButton("CloseButton", windowRect, templates);
        RectTransform closeRect = (RectTransform)closeButton.transform;
        closeRect.anchorMin = new Vector2(1f, 0f);
        closeRect.anchorMax = new Vector2(1f, 0f);
        closeRect.pivot = new Vector2(1f, 0f);
        closeRect.anchoredPosition = new Vector2(-28f, 18f);
        closeRect.sizeDelta = new Vector2(190f, 46f);
        closeButton.onClick.AddListener(Hide);

        buildFailureLogged = false;
        Diagnostics.Event("Shortcuts", "panel_built", "template=native_valheim_ui");
        return true;
    }

    private static void AddSectionHeading(string text, Color color, TMP_Text template)
    {
        TMP_Text heading = CreateText($"{text}Heading", contentRect!, template, layoutElement: true);
        heading.fontSize = 25f;
        heading.fontStyle = FontStyles.Bold;
        heading.color = color;
        heading.text = text;
    }

    private static void AddSpacer(float height)
    {
        RectTransform spacer = CreateRectObject("Spacer", contentRect!);
        LayoutElement layout = spacer.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private static void RefreshMultiplayerStatus(bool force = false)
    {
        if (multiplayerStatus == null || contentRect == null)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;
        if (!force && now < nextStatusRefreshAt)
        {
            return;
        }

        nextStatusRefreshAt = now + StatusRefreshInterval;
        InventoryCapabilitySnapshot snapshot = InventoryTransactions.GetCapabilitySnapshot();
        string fingerprint = snapshot.GetDisplayFingerprint();
        if (!force && string.Equals(fingerprint, lastStatusFingerprint, StringComparison.Ordinal))
        {
            return;
        }

        lastStatusFingerprint = fingerprint;
        multiplayerStatus.text = FormatMultiplayerStatus(snapshot);
        LayoutRebuilder.MarkLayoutForRebuild(contentRect);
    }

    private static string FormatMultiplayerStatus(InventoryCapabilitySnapshot snapshot)
    {
        if (snapshot.State == InventoryCapabilityState.Disconnected)
        {
            return "Put Away: local / not connected";
        }
        if (snapshot.State == InventoryCapabilityState.Checking)
        {
            return "Put Away: checking the server and player roster...";
        }
        if (snapshot.State == InventoryCapabilityState.ServerMissing)
        {
            return "Put Away: disabled\nServer — Benheim Inventory not detected · protocol — · incompatible";
        }

        string readiness = snapshot.IsReady ? "ready" : "disabled";
        string serverCompatibility = snapshot.ServerProtocol == InventoryTransactions.ProtocolVersion
            ? "compatible"
            : "incompatible";
        List<string> lines = new()
        {
            $"Put Away: {readiness}",
            $"Server — Benheim Inventory v{snapshot.ServerVersion} · protocol {snapshot.ServerProtocol} · detected · {serverCompatibility}",
        };
        if (snapshot.Players.Count == 0)
        {
            lines.Add("Players — no ready multiplayer peers");
        }
        else
        {
            foreach (InventoryPeerCapability player in snapshot.Players)
            {
                lines.Add(player.IsDetected
                    ? $"{player.PlayerName} — Benheim v{player.ClientVersion} · protocol {player.ProtocolVersion} · detected · {(player.IsCompatible ? "compatible" : "incompatible")}"
                    : $"{player.PlayerName} — Benheim not detected · protocol — · incompatible");
            }
        }

        return string.Join("\n", lines);
    }

    private readonly struct Entry
    {
        internal Entry(string key, string action, Color? accent = null)
        {
            Key = key;
            Action = action;
            Accent = accent;
        }

        internal string Key { get; }
        internal string Action { get; }
        internal Color? Accent { get; }
    }

    private sealed class Section
    {
        internal Section(string name, Color accent, Entry[] entries, string note)
        {
            Name = name;
            Accent = accent;
            Entries = entries;
            Note = note;
        }

        internal string Name { get; }
        internal Color Accent { get; }
        internal Entry[] Entries { get; }
        internal string Note { get; }
    }
}
