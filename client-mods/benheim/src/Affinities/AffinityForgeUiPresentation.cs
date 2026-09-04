using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Affinities;

internal sealed partial class AffinityForgeUi
{
    private void AddRow(int index, AffinityCatalogEntry entry, Player? player)
    {
        GameObject row = UnityEngine.Object.Instantiate(gui.m_recipeElementPrefab, gui.m_recipeListRoot);
        row.name = $"Benheim Affinity Row {index}";
        row.SetActive(true);
        RectTransform rowRect = row.transform as RectTransform
            ?? throw new InvalidOperationException("Affinity recipe donor is not a RectTransform.");
        rowRect.anchoredPosition = new Vector2(0f, index * -gui.m_recipeListSpace);
        ItemDrop? weapon = AffinityCatalog.WeaponDrop(entry);
        row.transform.Find("icon").GetComponent<Image>().sprite = weapon?.m_itemData.GetIcon();
        TMP_Text name = row.transform.Find("name").GetComponent<TMP_Text>();
        name.text = $"{WeaponName(entry)} · {AffinityPresentation.NameFor(entry.Affinity)}";
        name.color = CanApplyAnyOwnedWeapon(player, entry)
            ? Color.white
            : new Color(0.66f, 0.66f, 0.66f, 1f);
        row.transform.Find("Durability").GetComponent<GuiBar>().gameObject.SetActive(false);
        row.transform.Find("QualityLevel").GetComponent<TMP_Text>().gameObject.SetActive(false);
        int capturedIndex = index;
        row.GetComponent<Button>().onClick = new Button.ButtonClickedEvent();
        row.GetComponent<Button>().onClick.AddListener(delegate
        {
            selectedEntryIndex = capturedIndex;
            RefreshOwnedWeapons(null);
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

        Piece.Requirement requirement = new()
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
        weaponSelector.SetActive(false);
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

    private void RefreshOwnedWeapons(ItemDrop.ItemData? preferred)
    {
        ownedWeapons.Clear();
        AffinityCatalogEntry? entry = SelectedEntry();
        if (!entry.HasValue)
        {
            selectedWeaponIndex = 0;
            return;
        }

        AffinityCatalog.GetOwnedWeapons(Player.m_localPlayer, entry.Value, ownedWeapons);
        for (int index = 0; index < ownedWeapons.Count; index++)
        {
            ItemDrop.ItemData item = ownedWeapons[index];
            AffinityDiagnostics.Emit(
                DiagnosticEvent.Create("Affinity", "affinity_eligibility")
                    .String("weapon_prefab", entry.Value.WeaponPrefab)
                    .String("affinity", entry.Value.Affinity.ToString().ToLowerInvariant())
                    .Integer("quality", item.m_quality)
                    .Integer("max_quality", item.m_shared.m_maxQuality)
                    .Boolean("eligible", AffinityState.IsEligibleFor(item, entry.Value))
                    .Boolean(
                        "already_installed",
                        AffinityRules.IsSameAffinity(AffinityState.Read(item), entry.Value.Affinity)));
        }
        selectedWeaponIndex = preferred != null ? ownedWeapons.IndexOf(preferred) : -1;
        if (selectedWeaponIndex >= 0) return;

        selectedWeaponIndex = 0;
        for (int index = 0; index < ownedWeapons.Count; index++)
        {
            ItemDrop.ItemData item = ownedWeapons[index];
            if (!AffinityState.IsEligibleFor(item, entry.Value)) continue;
            if (AffinityRules.IsSameAffinity(AffinityState.Read(item), entry.Value.Affinity)) continue;
            selectedWeaponIndex = index;
            break;
        }
    }

    private bool CanApplyAnyOwnedWeapon(Player? player, AffinityCatalogEntry entry)
    {
        if (player == null
            || !AffinityApplication.IsAtBaseGameForge(player)
            || player.GetCurrentCraftingStation()?.CheckUsable(player, false) != true)
        {
            return false;
        }
        AffinityRequirementSpec requirement = AffinityPresentation.RequirementsFor(entry.Affinity);
        string resourceName = AffinityApplication.ResourceName(requirement);
        if (string.IsNullOrEmpty(resourceName)
            || player.GetInventory().CountItems(resourceName) < requirement.MaterialAmount)
        {
            return false;
        }

        List<ItemDrop.ItemData> inventoryItems = player.GetInventory().GetAllItems();
        for (int index = 0; index < inventoryItems.Count; index++)
        {
            ItemDrop.ItemData item = inventoryItems[index];
            if (AffinityState.IsEligibleFor(item, entry)
                && !AffinityRules.IsSameAffinity(AffinityState.Read(item), entry.Affinity))
            {
                return true;
            }
        }
        return false;
    }

    private AffinityCatalogEntry? SelectedEntry()
    {
        return selectedEntryIndex >= 0 && selectedEntryIndex < entries.Count
            ? entries[selectedEntryIndex]
            : null;
    }

    private ItemDrop.ItemData? SelectedWeapon()
    {
        return selectedWeaponIndex >= 0 && selectedWeaponIndex < ownedWeapons.Count
            ? ownedWeapons[selectedWeaponIndex]
            : null;
    }

    private static string WeaponName(AffinityCatalogEntry entry)
    {
        ItemDrop? weapon = AffinityCatalog.WeaponDrop(entry);
        return weapon != null ? Localize(weapon.m_itemData.m_shared.m_name) : entry.WeaponPrefab;
    }

    private static string ApplicationFailureText(string reason)
    {
        return reason switch
        {
            "no_local_player" => "No local player is available.",
            "no_item" => "No weapon is selected.",
            "item_moved" => "The selected weapon is no longer in your inventory.",
            "unsupported_affinity" => "That Affinity is not supported.",
            "ineligible_item" => "The selected weapon does not support that Affinity.",
            "maximum_quality_required" => "The selected weapon is not at maximum quality.",
            "affinity_already_installed" => "The selected weapon already has that Affinity.",
            "not_at_base_game_forge" => "You must use a base-game Forge.",
            "forge_unusable" => "The Forge is not usable right now.",
            "test_resource_unavailable" => "The required material is unavailable.",
            "missing_resources" => "You do not have the required materials.",
            "resource_consumption_mismatch" => "The material cost could not be charged safely.",
            _ => "An unexpected error occurred. No Affinity was applied.",
        };
    }

    private string ExactWeaponDescription(AffinityCatalogEntry entry, ItemDrop.ItemData? item)
    {
        if (item == null) return $"No {WeaponName(entry)} is available in your inventory.";

        string title = AffinityPresentation.InventoryTitle(
            Localize(item.m_shared.m_name),
            AffinityState.Read(item));
        return $"Selected weapon {selectedWeaponIndex + 1} of {ownedWeapons.Count}: {title}; "
            + $"quality {item.m_quality} of {item.m_shared.m_maxQuality}; "
            + $"inventory slot ({item.m_gridPos.x + 1}, {item.m_gridPos.y + 1}).";
    }

    private static string Localize(string value)
    {
        return Localization.instance != null ? Localization.instance.Localize(value) : value;
    }
}
