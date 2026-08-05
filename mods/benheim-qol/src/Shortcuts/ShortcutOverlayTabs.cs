using System;
using System.Collections.Generic;
using BenheimInventoryProtocol;
using BenheimQoL.Infrastructure;
using BenheimQoL.InventoryFeature;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Shortcuts;

internal static partial class ShortcutOverlay
{
    private static readonly List<TabState> Tabs = new();
    private static readonly Dictionary<ShortcutTab, GameObject> Pages = new();
    private static ShortcutTab activeTab = ShortcutTab.Controls;
    private static TMP_Text? multiplayerSummary;
    private static TMP_Text? multiplayerStatus;

    private static void BuildTabBar(RectTransform parent, NativeTemplates templates)
    {
        RectTransform bar = CreateRectObject("TabBar", parent);
        bar.anchorMin = new Vector2(0f, 1f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0.5f, 1f);
        bar.offsetMin = new Vector2(32f, -132f);
        bar.offsetMax = new Vector2(-32f, -82f);

        RectTransform buttons = CreateRectObject("TabButtons", bar);
        buttons.anchorMin = Vector2.zero;
        buttons.anchorMax = new Vector2(0.62f, 1f);
        buttons.offsetMin = Vector2.zero;
        buttons.offsetMax = new Vector2(-10f, 0f);
        HorizontalLayoutGroup layout = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        AddTab(buttons, templates, ShortcutTab.Controls, "Controls", ControlsAccent);
        AddTab(buttons, templates, ShortcutTab.Features, "Features", FeaturesAccent);
        AddTab(buttons, templates, ShortcutTab.Multiplayer, "Multiplayer", MultiplayerAccent);

        multiplayerSummary = CreateText("PutAwaySummary", bar, templates.Text, layoutElement: false);
        RectTransform summaryRect = (RectTransform)multiplayerSummary.transform;
        summaryRect.anchorMin = new Vector2(0.64f, 0f);
        summaryRect.anchorMax = Vector2.one;
        summaryRect.offsetMin = new Vector2(8f, 0f);
        summaryRect.offsetMax = Vector2.zero;
        multiplayerSummary.text = "Put Away: Checking...";
        multiplayerSummary.fontSize = 19f;
        multiplayerSummary.fontStyle = FontStyles.Bold;
        multiplayerSummary.alignment = TextAlignmentOptions.Right;

        RectTransform divider = CreateRectObject("Divider", parent);
        divider.anchorMin = new Vector2(0f, 1f);
        divider.anchorMax = new Vector2(1f, 1f);
        divider.pivot = new Vector2(0.5f, 1f);
        divider.offsetMin = new Vector2(32f, -142f);
        divider.offsetMax = new Vector2(-32f, -140f);
        Image line = divider.gameObject.AddComponent<Image>();
        line.color = new Color(0.62f, 0.5f, 0.3f, 0.75f);
        line.raycastTarget = false;
    }

    private static void AddTab(
        RectTransform parent,
        NativeTemplates templates,
        ShortcutTab tab,
        string label,
        Color accent)
    {
        Button button = CreateNativeButton($"{label}Tab", parent, templates, label);
        LayoutElement sizing = button.gameObject.AddComponent<LayoutElement>();
        sizing.minWidth = 92f;
        sizing.flexibleWidth = 1f;

        RectTransform indicator = CreateRectObject("ActiveIndicator", button.transform);
        indicator.anchorMin = Vector2.zero;
        indicator.anchorMax = new Vector2(1f, 0f);
        indicator.pivot = Vector2.zero;
        indicator.offsetMin = new Vector2(5f, 3f);
        indicator.offsetMax = new Vector2(-5f, 7f);
        Image indicatorImage = indicator.gameObject.AddComponent<Image>();
        indicatorImage.color = accent;
        indicatorImage.raycastTarget = false;

        button.onClick.AddListener(() => SelectTab(tab));
        Tabs.Add(new TabState(tab, button, indicator.gameObject));
    }

    private static void BuildPages(RectTransform parent, NativeTemplates templates)
    {
        GameObject controls = CreatePage("ControlsPage", parent);
        foreach (Section section in ControlSections)
        {
            AddSection(controls.transform as RectTransform, section, templates);
        }
        Pages.Add(ShortcutTab.Controls, controls);

        GameObject features = CreatePage("FeaturesPage", parent);
        foreach (Section section in FeatureSections)
        {
            AddSection(features.transform as RectTransform, section, templates);
        }
        Pages.Add(ShortcutTab.Features, features);

        GameObject multiplayer = CreatePage("MultiplayerPage", parent);
        RectTransform multiplayerRect = (RectTransform)multiplayer.transform;
        AddSectionHeading(multiplayerRect, "Put Away Compatibility", MultiplayerAccent, templates.Text);
        multiplayerStatus = CreateText("MultiplayerStatus", multiplayerRect, templates.Text, layoutElement: true);
        multiplayerStatus.fontSize = 20f;
        multiplayerStatus.color = Color.white;
        multiplayerStatus.richText = false;
        multiplayerStatus.text = "Checking the server and player roster...";
        TMP_Text note = CreateText("MultiplayerNote", multiplayerRect, templates.Text, layoutElement: true);
        note.fontSize = 18f;
        note.color = new Color(0.78f, 0.8f, 0.82f, 1f);
        note.text = "Put Away is disabled when the server or a ready player uses an incompatible transaction protocol.";
        Pages.Add(ShortcutTab.Multiplayer, multiplayer);

        SelectTab(activeTab, force: true);
    }

    private static GameObject CreatePage(string name, RectTransform parent)
    {
        RectTransform page = CreateRectObject(name, parent);
        VerticalLayoutGroup layout = page.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = page.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return page.gameObject;
    }

    private static void AddSection(RectTransform? parent, Section section, NativeTemplates templates)
    {
        if (parent == null)
        {
            return;
        }

        AddSectionHeading(parent, section.Name, section.Accent, templates.Text);
        foreach (Entry entry in section.Entries)
        {
            AddKeyActionRow(parent, entry, section.Accent, templates.Text);
        }

        TMP_Text note = CreateText($"{section.Name}Note", parent, templates.Text, layoutElement: true);
        note.fontSize = 17f;
        note.color = new Color(0.78f, 0.8f, 0.82f, 1f);
        note.text = section.Note;
        AddSpacer(parent, 7f);
    }

    private static void AddSectionHeading(
        RectTransform parent,
        string value,
        Color color,
        TMP_Text template)
    {
        TMP_Text heading = CreateText($"{value}Heading", parent, template, layoutElement: true);
        heading.fontSize = 25f;
        heading.fontStyle = FontStyles.Bold;
        heading.color = color;
        heading.text = value;
    }

    private static void AddKeyActionRow(
        RectTransform parent,
        Entry entry,
        Color accent,
        TMP_Text template)
    {
        RectTransform row = CreateRectObject("KeyActionRow", parent);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        LayoutElement rowSize = row.gameObject.AddComponent<LayoutElement>();
        rowSize.minHeight = 29f;

        TMP_Text key = CreateText("Key", row, template, layoutElement: true);
        key.text = entry.Key;
        key.fontSize = 19f;
        key.fontStyle = FontStyles.Bold;
        key.color = accent;
        LayoutElement keySize = key.GetComponent<LayoutElement>();
        keySize.minWidth = 230f;
        keySize.preferredWidth = 230f;
        keySize.flexibleWidth = 0f;

        TMP_Text action = CreateText("Action", row, template, layoutElement: true);
        action.text = entry.Action;
        action.fontSize = 19f;
        action.color = Color.white;
        LayoutElement actionSize = action.GetComponent<LayoutElement>();
        actionSize.flexibleWidth = 1f;
    }

    private static void AddSpacer(RectTransform parent, float height)
    {
        RectTransform spacer = CreateRectObject("Spacer", parent);
        LayoutElement layout = spacer.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private static void SelectTab(ShortcutTab tab, bool force = false)
    {
        if (!force && tab == activeTab)
        {
            return;
        }

        activeTab = tab;
        foreach (KeyValuePair<ShortcutTab, GameObject> page in Pages)
        {
            page.Value.SetActive(page.Key == tab);
        }
        foreach (TabState state in Tabs)
        {
            state.Indicator.SetActive(state.Tab == tab);
        }

        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }
        if (contentScroll != null)
        {
            contentScroll.verticalNormalizedPosition = 1f;
        }
        Diagnostics.Event("Shortcuts", "tab_selected", $"tab={tab.ToString().ToLowerInvariant()}");
    }

    private static void RefreshMultiplayerStatus(bool force = false)
    {
        if (multiplayerSummary == null || multiplayerStatus == null || contentRect == null)
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
        multiplayerSummary.text = FormatMultiplayerSummary(snapshot);
        multiplayerSummary.color = SummaryColor(snapshot);
        multiplayerStatus.text = FormatMultiplayerStatus(snapshot);
        LayoutRebuilder.MarkLayoutForRebuild(contentRect);
    }

    private static string FormatMultiplayerSummary(InventoryCapabilitySnapshot snapshot)
    {
        return snapshot.State switch
        {
            InventoryCapabilityState.Disconnected => "Put Away: Offline",
            InventoryCapabilityState.Checking => "Put Away: Checking...",
            _ => snapshot.IsReady ? "Put Away: Ready" : "Put Away: Disabled",
        };
    }

    private static Color SummaryColor(InventoryCapabilitySnapshot snapshot)
    {
        if (snapshot.IsReady)
        {
            return new Color(0.5f, 0.9f, 0.55f, 1f);
        }
        if (snapshot.State == InventoryCapabilityState.Checking)
        {
            return ControlsAccent;
        }
        return new Color(1f, 0.5f, 0.45f, 1f);
    }

    private static string FormatMultiplayerStatus(InventoryCapabilitySnapshot snapshot)
    {
        if (snapshot.State == InventoryCapabilityState.Disconnected)
        {
            return "Not connected to a multiplayer server.";
        }
        if (snapshot.State == InventoryCapabilityState.Checking)
        {
            return "Checking the server and player roster...";
        }
        if (snapshot.State == InventoryCapabilityState.ServerMissing)
        {
            return "Disabled\n\nServer\nBenheim Inventory not detected\nProtocol: unknown\nCompatibility: incompatible";
        }

        string serverCompatibility = snapshot.ServerProtocol == InventoryTransactions.ProtocolVersion
            ? "compatible"
            : "incompatible";
        List<string> lines = new()
        {
            snapshot.IsReady ? "Ready" : "Disabled",
            string.Empty,
            "Server",
            $"Benheim Inventory v{snapshot.ServerVersion}  |  Protocol {snapshot.ServerProtocol}  |  {serverCompatibility}",
            string.Empty,
            "Players",
        };
        if (snapshot.Players.Count == 0)
        {
            lines.Add("No ready multiplayer peers");
        }
        else
        {
            foreach (InventoryPeerCapability player in snapshot.Players)
            {
                lines.Add(player.IsDetected
                    ? $"{player.PlayerName}  |  Benheim v{player.ClientVersion}  |  Protocol {player.ProtocolVersion}  |  {(player.IsCompatible ? "compatible" : "incompatible")}"
                    : $"{player.PlayerName}  |  Benheim not detected  |  incompatible");
            }
        }
        return string.Join("\n", lines);
    }

    private static void ResetTabState()
    {
        Tabs.Clear();
        Pages.Clear();
        multiplayerSummary = null;
        multiplayerStatus = null;
        activeTab = ShortcutTab.Controls;
    }

    private enum ShortcutTab
    {
        Controls,
        Features,
        Multiplayer,
    }

    private readonly struct TabState
    {
        internal TabState(ShortcutTab tab, Button button, GameObject indicator)
        {
            Tab = tab;
            Button = button;
            Indicator = indicator;
        }

        internal ShortcutTab Tab { get; }
        internal Button Button { get; }
        internal GameObject Indicator { get; }
    }

}
