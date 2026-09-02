using System;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Affinities;

internal sealed partial class AffinityForgeUi
{
    private static readonly FieldInfo? CraftTimerField =
        AccessTools.Field(typeof(InventoryGui), "m_craftTimer");
    private static readonly MethodInfo? UpdateCraftingPanelMethod =
        AccessTools.Method(typeof(InventoryGui), "UpdateCraftingPanel");

    private void Initialize(InventoryGui inventoryGui)
    {
        gui = inventoryGui;
        ValidateDonors();
        minStationLevelBaseColor = gui.m_minStationLevelText.color;
        restoreMinStationLevelSprite = gui.m_minStationLevelIcon.sprite;
        GameObject tabObject = UnityEngine.Object.Instantiate(
            gui.m_tabUpgrade.gameObject,
            gui.m_tabUpgrade.transform.parent);
        tabObject.name = "Benheim Affinity Tab";
        affinityTab = tabObject.GetComponent<Button>();
        affinityTab.onClick = new Button.ButtonClickedEvent();
        affinityTab.onClick.AddListener(Enter);
        SetTabText(tabObject.transform, "Affinity");
        PlaceAfterUpgrade(
            tabObject.transform as RectTransform
            ?? throw new InvalidOperationException("Affinity tab donor is not a RectTransform."));
        RemoveClonedGamepadBindings(tabObject);
        restoreUpgradeNavigation = gui.m_tabUpgrade.navigation;
        upgradeNavigationCaptured = true;
        ConfigureAffinityNavigation();
        affinityTab.gameObject.SetActive(false);
    }

    private void ValidateDonors()
    {
        if (CraftTimerField == null || UpdateCraftingPanelMethod == null)
        {
            throw new MissingMemberException("Affinity requires InventoryGui crafting lifecycle members.");
        }
        if (gui.m_tabCraft == null
            || gui.m_tabUpgrade == null
            || gui.m_tabUpgrade.transform.parent == null
            || gui.m_recipeElementPrefab == null
            || gui.m_recipeListRoot == null
            || gui.m_recipeListScroll == null
            || gui.m_recipeEnsureVisible == null
            || gui.m_recipeRequirementList == null
            || gui.m_recipeRequirementList.Length == 0
            || gui.m_recipeRequirementList[0] == null
            || gui.m_recipeIcon == null
            || gui.m_recipeName == null
            || gui.m_recipeDecription == null
            || gui.m_itemCraftType == null
            || gui.m_minStationLevelIcon == null
            || gui.m_minStationLevelText == null
            || gui.m_craftButton == null)
        {
            throw new InvalidOperationException("Affinity requires native Forge UI donors.");
        }
    }

    private static void SetTabText(Transform root, string value)
    {
        TMP_Text[] labels = root.GetComponentsInChildren<TMP_Text>(includeInactive: true);
        for (int index = 0; index < labels.Length; index++) labels[index].text = value;
    }

    private void PlaceAfterUpgrade(RectTransform affinity)
    {
        RectTransform craft = gui.m_tabCraft.transform as RectTransform
            ?? throw new InvalidOperationException("Craft tab is not a RectTransform.");
        RectTransform upgrade = gui.m_tabUpgrade.transform as RectTransform
            ?? throw new InvalidOperationException("Upgrade tab is not a RectTransform.");
        Vector2 spacing = upgrade.anchoredPosition - craft.anchoredPosition;
        if (spacing.sqrMagnitude < 1f) spacing = new Vector2(upgrade.rect.width, 0f);
        affinity.anchoredPosition = upgrade.anchoredPosition + spacing;
    }

    private static void RemoveClonedGamepadBindings(GameObject tabObject)
    {
        UIGamePad[] bindings = tabObject.GetComponentsInChildren<UIGamePad>(includeInactive: true);
        for (int index = 0; index < bindings.Length; index++)
        {
            UnityEngine.Object.Destroy(bindings[index]);
        }
    }

    private void ConfigureAffinityNavigation()
    {
        Navigation affinity = affinityTab.navigation;
        affinity.mode = Navigation.Mode.Explicit;
        affinity.selectOnLeft = gui.m_tabUpgrade;
        affinityTab.navigation = affinity;
    }

    private void SetAffinityNavigationAvailable(bool available)
    {
        if (!upgradeNavigationCaptured) return;
        if (!available)
        {
            gui.m_tabUpgrade.navigation = restoreUpgradeNavigation;
            return;
        }

        Navigation upgrade = gui.m_tabUpgrade.navigation;
        upgrade.mode = Navigation.Mode.Explicit;
        upgrade.selectOnRight = affinityTab;
        gui.m_tabUpgrade.navigation = upgrade;
    }
}
