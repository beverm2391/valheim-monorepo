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
    private readonly List<AffinityCatalogEntry> entries = new();
    private readonly List<ItemDrop.ItemData> ownedWeapons = new();
    private InventoryGui gui = null!;
    private Button affinityTab = null!;
    private GameObject weaponSelector = null!;
    private Button previousWeapon = null!;
    private Button nextWeapon = null!;
    private TMP_Text weaponIndex = null!;
    private int selectedEntryIndex;
    private int selectedWeaponIndex;
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
        weaponSelector.SetActive(false);
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

        AffinityCatalogEntry? previousEntry = SelectedEntry();
        ItemDrop.ItemData? previousWeapon = SelectedWeapon();
        HideNativeRows();
        ClearRows();
        entries.Clear();

        Player? player = Player.m_localPlayer;
        AffinityCatalog.GetUnlocked(player, entries);
        selectedEntryIndex = 0;
        if (previousEntry.HasValue)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (!entries[index].Matches(previousEntry.Value)) continue;
                selectedEntryIndex = index;
                break;
            }
        }
        for (int index = 0; index < entries.Count; index++)
        {
            AddRow(index, entries[index], player);
        }
        float height = Mathf.Max(gui.m_recipeListRoot.rect.height, entries.Count * gui.m_recipeListSpace);
        gui.m_recipeListRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        gui.m_recipeListScroll.value = 1f;
        RefreshOwnedWeapons(previousWeapon);
        Render();
        if (focusSelection && rows.Count > 0)
        {
            gui.m_recipeEnsureVisible.CenterOnItem(rows[selectedEntryIndex].transform as RectTransform);
        }
    }

    internal void Render()
    {
        if (!active) return;
        for (int index = 0; index < rows.Count; index++)
        {
            Transform? selection = rows[index].transform.Find("selected");
            if (selection != null) selection.gameObject.SetActive(index == selectedEntryIndex);
        }

        Player? player = Player.m_localPlayer;
        AffinityCatalogEntry? selectedEntry = SelectedEntry();
        if (player == null || !selectedEntry.HasValue)
        {
            gui.m_recipeIcon.enabled = false;
            gui.m_recipeName.enabled = true;
            gui.m_recipeName.text = "No unlocked Affinities";
            gui.m_recipeDecription.enabled = true;
            gui.m_recipeDecription.text =
                "Discover a supported weapon recipe and the materials required by an Affinity to reveal that Affinity here.";
            gui.m_itemCraftType.gameObject.SetActive(false);
            weaponSelector.SetActive(false);
            gui.m_craftButton.interactable = false;
            HideRequirements();
            return;
        }

        AffinityCatalogEntry entry = selectedEntry.Value;
        AffinityLoadResult selectedAffinity = entry.Affinity;
        ItemDrop? weaponDrop = AffinityCatalog.WeaponDrop(entry);
        ItemDrop.ItemData? item = SelectedWeapon();
        AffinityLoadResult existing = AffinityState.Read(item);
        string affinityName = AffinityPresentation.NameFor(selectedAffinity);
        gui.m_recipeIcon.enabled = true;
        gui.m_recipeIcon.sprite = weaponDrop?.m_itemData.GetIcon();
        gui.m_recipeName.enabled = true;
        gui.m_recipeName.text = $"{WeaponName(entry)} · {affinityName}";
        gui.m_recipeDecription.enabled = true;
        gui.m_recipeDecription.text =
            AffinityPresentation.BehaviorDescription(
                selectedAffinity, LungeRuntime.Force, LungeRuntime.MinimumVerticalVelocity) +
            (selectedAffinity == AffinityLoadResult.Test
                ? "\n\nCost: 1 Wood.\n\n"
                : "\n\nCurrent test cost: 1 Wood. The final cost is not set.\n\n") +
            "The Affinity stays with this item. Replacement destroys the old Affinity and all prior investment. No refund.";
        gui.m_itemCraftType.gameObject.SetActive(true);
        gui.m_itemCraftType.text = ExactWeaponDescription(entry, item);
        weaponSelector.SetActive(ownedWeapons.Count > 1);
        weaponIndex.text = ownedWeapons.Count == 0
            ? "0 / 0"
            : $"{selectedWeaponIndex + 1} / {ownedWeapons.Count}";
        previousWeapon.interactable = selectedWeaponIndex > 0;
        nextWeapon.interactable = selectedWeaponIndex + 1 < ownedWeapons.Count;
        gui.m_variantButton.gameObject.SetActive(false);
        gui.m_qualityPanel.gameObject.SetActive(false);
        gui.m_craftProgressPanel.gameObject.SetActive(false);
        gui.m_craftButton.gameObject.SetActive(true);

        AffinityRequirementSpec requirement = AffinityPresentation.RequirementsFor(selectedAffinity);
        int owned = ShowRequirements(player, requirement);
        bool canApply = AffinityApplication.IsAtBaseGameForge(player)
            && player.GetCurrentCraftingStation()?.CheckUsable(player, false) == true
            && item != null
            && player.GetInventory().ContainsItem(item)
            && AffinityState.IsEligibleFor(item, entry)
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
                    : item == null
                        ? $"Carry a {WeaponName(entry)} to apply this Affinity."
                        : !AffinityState.IsEligibleFor(item, entry)
                            ? $"{affinityName} requires a max-quality {WeaponName(entry)}."
                            : owned < requirement.MaterialAmount
                                ? "You need 1 Wood."
                                : "You need the selected eligible weapon and access to a working Forge.";
        }
    }

    internal void Navigate(int direction)
    {
        if (!active || UnifiedPopup.IsVisible() || rows.Count == 0) return;
        selectedEntryIndex = Mathf.Clamp(selectedEntryIndex + direction, 0, rows.Count - 1);
        RefreshOwnedWeapons(null);
        Render();
        gui.m_recipeEnsureVisible.CenterOnItem(rows[selectedEntryIndex].transform as RectTransform);
    }

    internal void NavigateWeapon(int direction)
    {
        if (!active || UnifiedPopup.IsVisible() || ownedWeapons.Count < 2) return;
        selectedWeaponIndex = Mathf.Clamp(
            selectedWeaponIndex + direction,
            0,
            ownedWeapons.Count - 1);
        Render();
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
        if (InputState.IsButtonDown("JoyLStickLeft") || InputState.IsButtonDown("JoyDPadLeft"))
        {
            NavigateWeapon(-1);
        }
        if (InputState.IsButtonDown("JoyLStickRight") || InputState.IsButtonDown("JoyDPadRight"))
        {
            NavigateWeapon(1);
        }
    }

    internal void ConfirmApply()
    {
        ItemDrop.ItemData? captured = SelectedWeapon();
        AffinityCatalogEntry? entry = SelectedEntry();
        if (captured == null || !entry.HasValue || UnifiedPopup.IsVisible()) return;

        AffinityLoadResult selectedAffinity = entry.Value.Affinity;
        if (!AffinityState.IsEligibleFor(captured, entry.Value)) return;

        // The disabled button is the visible guard. Recheck here so indirect
        // invocation cannot turn the same Affinity into a replacement attempt.
        if (AffinityRules.IsSameAffinity(AffinityState.Read(captured), selectedAffinity))
        {
            return;
        }

        bool replacing = AffinityState.Load(captured, "forge_confirmation") != AffinityLoadResult.None;
        AffinityRequirementSpec requirement = AffinityPresentation.RequirementsFor(selectedAffinity);
        string body =
            $"Apply {AffinityPresentation.NameFor(selectedAffinity)} to the selected {Localize(captured.m_shared.m_name)} for {requirement.MaterialAmount} Wood?\n\n" +
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
                $"Affinity was not applied: {ApplicationFailureText(result.Reason)}");
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

}
