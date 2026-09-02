using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Affinities;

internal sealed partial class AffinityForgeUi : MonoBehaviour
{
    private readonly List<GameObject> rows = new();
    private readonly List<ItemDrop.ItemData> items = new();
    private InventoryGui gui = null!;
    private Button affinityTab = null!;
    private int selectedIndex;
    private bool active;
    private bool restoreCraftTab = true;
    private float restoreScrollValue = 1f;
    private Color minStationLevelBaseColor;
    private Sprite restoreMinStationLevelSprite = null!;
    private Navigation restoreUpgradeNavigation;
    private bool upgradeNavigationCaptured;

    internal bool Active => active;

    internal static AffinityForgeUi Attach(InventoryGui inventoryGui)
    {
        AffinityForgeUi controller = inventoryGui.gameObject.AddComponent<AffinityForgeUi>();
        controller.Initialize(inventoryGui);
        return controller;
    }

    internal static AffinityForgeUi? Find(InventoryGui inventoryGui)
    {
        return inventoryGui != null ? inventoryGui.GetComponent<AffinityForgeUi>() : null;
    }

    internal void UpdateAvailability()
    {
        bool available = AffinityApplication.IsAtBaseGameForge(Player.m_localPlayer);
        affinityTab.gameObject.SetActive(available);
        SetAffinityNavigationAvailable(available);
        if (active && !available)
        {
            LeaveForNative();
        }
    }

    internal void Enter()
    {
        if (active || !AffinityApplication.IsAtBaseGameForge(Player.m_localPlayer)) return;
        if (CraftTimerField == null || (float)CraftTimerField.GetValue(gui) >= 0f)
        {
            Player.m_localPlayer?.Message(
                MessageHud.MessageType.Center,
                "Finish or cancel the current craft before opening Affinity.");
            return;
        }

        restoreCraftTab = gui.InCraftTab();
        restoreScrollValue = gui.m_recipeListScroll.value;
        active = true;
        gui.m_tabCraft.interactable = true;
        gui.m_tabUpgrade.interactable = true;
        affinityTab.interactable = false;
        AffinityDiagnostics.Emit(
            DiagnosticEvent.Create("Affinity", "affinity_menu_discovered")
                .String("station", AffinityPresentation.ForgeNameToken)
                .String("tab", "affinity"));
        Refresh(focusSelection: true);
    }

    internal void LeaveForNative()
    {
        if (!active) return;
        active = false;
        ClearRows();
        affinityTab.interactable = true;
        gui.m_tabCraft.interactable = !restoreCraftTab;
        gui.m_tabUpgrade.interactable = restoreCraftTab;
        gui.m_recipeListScroll.value = restoreScrollValue;
        RestoreNativeDisplayDefaults();
    }

    internal void LeaveAndRebuildNative()
    {
        if (!active) return;
        LeaveForNative();
        UpdateCraftingPanelMethod?.Invoke(gui, new object[] { false });
    }

    internal void Refresh(bool focusSelection = false)
    {
        if (!active) return;

        ItemDrop.ItemData? selected = selectedIndex >= 0 && selectedIndex < items.Count
            ? items[selectedIndex]
            : null;
        HideNativeRows();
        ClearRows();
        items.Clear();

        Player? player = Player.m_localPlayer;
        if (player != null)
        {
            List<ItemDrop.ItemData> inventoryItems = player.GetInventory().GetAllItems();
            for (int index = 0; index < inventoryItems.Count; index++)
            {
                ItemDrop.ItemData item = inventoryItems[index];
                bool canonical = AffinityState.IsCanonicalPrefab(item, AffinityState.ClubPrefab)
                    || AffinityState.IsCanonicalPrefab(item, AffinityState.SnipeBowPrefab);
                if (!canonical) continue;
                AffinityLoadResult available = AffinityState.AvailableFor(item);
                bool eligible = available != AffinityLoadResult.None;
                AffinityDiagnostics.Emit(
                    DiagnosticEvent.Create("Affinity", "affinity_eligibility")
                        .String("source", "forge_menu")
                        .String("affinity", available.ToString().ToLowerInvariant())
                        .String("item_prefab", AffinityState.ItemPrefab(item))
                        .Boolean("eligible", eligible)
                        .Integer("quality", item.m_quality)
                        .Integer("max_quality", item.m_shared.m_maxQuality));
                if (eligible) items.Add(item);
            }
        }

        selectedIndex = selected != null ? items.IndexOf(selected) : 0;
        if (selectedIndex < 0) selectedIndex = 0;
        for (int index = 0; index < items.Count; index++)
        {
            AddRow(index, items[index]);
        }
        float height = Mathf.Max(gui.m_recipeListRoot.rect.height, items.Count * gui.m_recipeListSpace);
        gui.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        gui.m_recipeListScroll.value = 1f;
        Render();
        if (focusSelection && rows.Count > 0)
        {
            gui.m_recipeEnsureVisible.CenterOnItem(rows[selectedIndex].transform as RectTransform);
        }
    }

    internal void Render()
    {
        if (!active) return;
        for (int index = 0; index < rows.Count; index++)
        {
            Transform? selection = rows[index].transform.Find("selected");
            if (selection != null) selection.gameObject.SetActive(index == selectedIndex);
        }

        Player? player = Player.m_localPlayer;
        ItemDrop.ItemData? item = SelectedItem();
        AffinityLoadResult selectedAffinity = AffinityState.AvailableFor(item);
        if (player == null || item == null || selectedAffinity == AffinityLoadResult.None)
        {
            gui.m_recipeIcon.enabled = false;
            gui.m_recipeName.enabled = true;
            gui.m_recipeName.text = "No eligible weapons";
            gui.m_recipeDecription.enabled = true;
            gui.m_recipeDecription.text =
                "Use a max-quality base-game Club for Lunge or Huntsman Bow for Snipe.";
            gui.m_itemCraftType.gameObject.SetActive(false);
            gui.m_craftButton.interactable = false;
            HideRequirements();
            return;
        }

        AffinityLoadResult existing = AffinityState.Read(item);
        string affinityName = AffinityPresentation.NameFor(selectedAffinity);
        gui.m_recipeIcon.enabled = true;
        gui.m_recipeIcon.sprite = item.GetIcon();
        gui.m_recipeName.enabled = true;
        gui.m_recipeName.text = $"{Localize(item.m_shared.m_name)} · {affinityName}";
        gui.m_recipeDecription.enabled = true;
        gui.m_recipeDecription.text =
            AffinityPresentation.BehaviorDescription(
                selectedAffinity, LungeRuntime.Force, LungeRuntime.MinimumVerticalVelocity) +
            "\n\nTEMPORARY TEST COST: 1 Wood. This is not final balance.\n\n" +
            "The Affinity stays with this item. Replacement destroys the old Affinity and all prior investment. No refund.";
        gui.m_itemCraftType.gameObject.SetActive(true);
        gui.m_itemCraftType.text =
            $"Exact item: quality {item.m_quality}, slot {item.m_gridPos.x + 1},{item.m_gridPos.y + 1}";
        gui.m_variantButton.gameObject.SetActive(false);
        gui.m_qualityPanel.gameObject.SetActive(false);
        gui.m_craftProgressPanel.gameObject.SetActive(false);
        gui.m_craftButton.gameObject.SetActive(true);

        AffinityRequirementSpec requirement = AffinityPresentation.RequirementsFor(selectedAffinity);
        int owned = ShowRequirements(player, requirement);
        bool canApply = AffinityApplication.IsAtBaseGameForge(player)
            && player.GetInventory().ContainsItem(item)
            && selectedAffinity != AffinityLoadResult.None
            && !AffinityRules.IsSameAffinity(existing, selectedAffinity)
            && owned >= requirement.MaterialAmount;
        gui.m_craftButton.interactable = canApply;
        gui.m_craftButton.GetComponentInChildren<TMP_Text>().text =
            AffinityRules.IsSameAffinity(existing, selectedAffinity)
                ? $"{affinityName} Applied"
                : existing == AffinityLoadResult.None
                    ? $"Apply {affinityName}"
                    : $"Replace with {affinityName}";
        UITooltip? tooltip = gui.m_craftButton.GetComponent<UITooltip>();
        if (tooltip != null)
        {
            tooltip.m_text = canApply
                ? string.Empty
                : AffinityRules.IsSameAffinity(existing, selectedAffinity)
                    ? $"This exact item already has {affinityName}."
                    : "Requires the exact eligible item, the Forge, and 1 Wood.";
        }
    }

    internal void Navigate(int direction)
    {
        if (!active || UnifiedPopup.IsVisible() || rows.Count == 0) return;
        selectedIndex = Mathf.Clamp(selectedIndex + direction, 0, rows.Count - 1);
        Render();
        gui.m_recipeEnsureVisible.CenterOnItem(rows[selectedIndex].transform as RectTransform);
    }

    internal void HandleGamepadInput()
    {
        if (InputState.IsButtonDown("JoyLStickDown") || InputState.IsButtonDown("JoyDPadDown"))
        {
            Navigate(1);
        }
        if (InputState.IsButtonDown("JoyLStickUp") || InputState.IsButtonDown("JoyDPadUp"))
        {
            Navigate(-1);
        }
    }

    internal void ConfirmApply()
    {
        ItemDrop.ItemData? captured = SelectedItem();
        if (captured == null || UnifiedPopup.IsVisible()) return;

        AffinityLoadResult selectedAffinity = AffinityState.AvailableFor(captured);
        if (selectedAffinity == AffinityLoadResult.None) return;

        // The disabled button is the visible guard. Recheck here so indirect
        // invocation cannot turn the same Affinity into a replacement attempt.
        if (AffinityRules.IsSameAffinity(AffinityState.Read(captured), selectedAffinity))
        {
            return;
        }

        bool replacing = AffinityState.Load(captured, "forge_confirmation") != AffinityLoadResult.None;
        string body =
            $"Apply {AffinityPresentation.NameFor(selectedAffinity)} to this exact {Localize(captured.m_shared.m_name)} for the temporary test cost of 1 Wood?\n\n" +
            "The cost is nonrefundable." +
            (replacing
                ? " The old Affinity and every material previously spent on it will be destroyed with no refund."
                : string.Empty);
        UnifiedPopup.Push(new YesNoPopup(
            "Apply Affinity",
            body,
            delegate
            {
                UnifiedPopup.Pop();
                CompleteApply(captured, selectedAffinity);
            },
            UnifiedPopup.Pop,
            localizeText: false));
        UnifiedPopup.SetFocus();
    }

    private void CompleteApply(ItemDrop.ItemData captured, AffinityLoadResult selectedAffinity)
    {
        AffinityApplicationResult result = AffinityApplication.Apply(
            Player.m_localPlayer,
            captured,
            selectedAffinity,
            requireForge: true,
            consumeResources: true,
            source: "forge");
        if (!result.Applied)
        {
            Player.m_localPlayer?.Message(
                MessageHud.MessageType.Center,
                $"Affinity was not applied: {result.Reason}.");
            Refresh();
            return;
        }

        Player? player = Player.m_localPlayer;
        CraftingStation? forge = player?.GetCurrentCraftingStation();
        if (player != null && forge != null)
        {
            forge.m_craftItemDoneEffects.Create(player.transform.position, Quaternion.identity);
            player.Message(
                MessageHud.MessageType.Center,
                result.Replacing
                    ? $"{AffinityPresentation.NameFor(selectedAffinity)} replaced the previous Affinity. Prior investment was not refunded."
                    : $"{AffinityPresentation.NameFor(selectedAffinity)} applied. The 1 Wood cost is nonrefundable.");
        }
        Refresh();
    }

    private void AddRow(int index, ItemDrop.ItemData item)
    {
        GameObject row = UnityEngine.Object.Instantiate(gui.m_recipeElementPrefab, gui.m_recipeListRoot);
        row.name = $"Benheim Affinity Row {index}";
        row.SetActive(true);
        RectTransform rowRect = row.transform as RectTransform
            ?? throw new InvalidOperationException("Affinity recipe donor is not a RectTransform.");
        rowRect.anchoredPosition = new Vector2(0f, index * -gui.m_recipeListSpace);
        row.transform.Find("icon").GetComponent<Image>().sprite = item.GetIcon();
        TMP_Text name = row.transform.Find("name").GetComponent<TMP_Text>();
        name.text = $"{Localize(item.m_shared.m_name)} · {AffinityPresentation.NameFor(AffinityState.AvailableFor(item))}";
        name.color = Color.white;
        GuiBar durability = row.transform.Find("Durability").GetComponent<GuiBar>();
        bool showDurability = item.m_shared.m_useDurability
            && item.m_durability < item.GetMaxDurability();
        durability.gameObject.SetActive(showDurability);
        if (showDurability) durability.SetValue(item.GetDurabilityPercentage());
        TMP_Text quality = row.transform.Find("QualityLevel").GetComponent<TMP_Text>();
        quality.gameObject.SetActive(true);
        quality.text = item.m_quality.ToString();
        int capturedIndex = index;
        row.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
        row.GetComponent<Button>().onClick.AddListener(delegate
        {
            selectedIndex = capturedIndex;
            Render();
        });
        rows.Add(row);
    }

    private int ShowRequirements(Player player, AffinityRequirementSpec specification)
    {
        ItemDrop? resource = AffinityApplication.ResourceDrop(specification);
        CraftingStation? station = player.GetCurrentCraftingStation();
        if (resource == null || station == null)
        {
            HideRequirements();
            return 0;
        }

        gui.m_minStationLevelIcon.sprite = station.m_icon;
        gui.m_minStationLevelIcon.gameObject.SetActive(true);
        gui.m_minStationLevelText.text = specification.StationLevel.ToString();
        gui.m_minStationLevelText.color = station.GetLevel() >= specification.StationLevel
            ? minStationLevelBaseColor
            : Color.red;

        Piece.Requirement requirement = new Piece.Requirement
        {
            m_resItem = resource,
            m_amount = specification.MaterialAmount,
        };
        InventoryGui.SetupRequirement(
            gui.m_recipeRequirementList[0].transform,
            requirement,
            player,
            craft: true,
            quality: 1);
        string resourceName = resource.m_itemData.m_shared.m_name;
        int owned = player.GetInventory().CountItems(resourceName);
        TMP_Text amount = gui.m_recipeRequirementList[0].transform
            .Find("res_amount")
            .GetComponent<TMP_Text>();
        amount.text = $"{owned}/{specification.MaterialAmount}";
        amount.color = owned >= specification.MaterialAmount ? Color.white : Color.red;
        for (int index = 1; index < gui.m_recipeRequirementList.Length; index++)
        {
            InventoryGui.HideRequirement(gui.m_recipeRequirementList[index].transform);
        }
        return owned;
    }

    private void HideNativeRows()
    {
        for (int index = 0; index < gui.m_recipeListRoot.childCount; index++)
        {
            GameObject child = gui.m_recipeListRoot.GetChild(index).gameObject;
            if (!rows.Contains(child)) child.SetActive(false);
        }
    }

    private void ClearRows()
    {
        for (int index = 0; index < rows.Count; index++)
        {
            if (rows[index] != null) UnityEngine.Object.Destroy(rows[index]);
        }
        rows.Clear();
    }

    private void HideRequirements()
    {
        gui.m_minStationLevelIcon.gameObject.SetActive(false);
        for (int index = 0; index < gui.m_recipeRequirementList.Length; index++)
        {
            InventoryGui.HideRequirement(gui.m_recipeRequirementList[index].transform);
        }
    }

    private void RestoreNativeDisplayDefaults()
    {
        gui.m_minStationLevelIcon.sprite = restoreMinStationLevelSprite;
        gui.m_minStationLevelText.color = minStationLevelBaseColor;
        gui.m_recipeIcon.enabled = false;
        gui.m_recipeName.enabled = false;
        gui.m_recipeDecription.enabled = false;
        gui.m_itemCraftType.gameObject.SetActive(false);
        gui.m_variantButton.gameObject.SetActive(false);
        gui.m_qualityPanel.gameObject.SetActive(false);
        gui.m_craftProgressPanel.gameObject.SetActive(false);
        HideRequirements();
    }

    private ItemDrop.ItemData? SelectedItem()
    {
        return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null;
    }

    private static string Localize(string value)
    {
        return Localization.instance != null ? Localization.instance.Localize(value) : value;
    }

}
