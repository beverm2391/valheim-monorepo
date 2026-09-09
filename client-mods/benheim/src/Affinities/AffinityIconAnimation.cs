using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BenheimQoL.Affinities;

/// <summary>
/// Adds one quiet pulse to the exact native item sprite that owns an Affinity.
/// The duplicate inherits the native sprite material and leaves the base icon,
/// selection, equipped, queued, durability, and amount indicators untouched.
/// </summary>
internal static class AffinityIconAnimation
{
    private const string OverlayName = "benheim-affinity-sprite-pulse";

    private static readonly FieldInfo InventoryElementsField =
        AccessTools.Field(typeof(InventoryGrid), "m_elements");
    private static readonly FieldInfo InventoryElementPositionField =
        AccessTools.Field(AccessTools.Inner(typeof(InventoryGrid), "Element"), "m_pos");
    private static readonly FieldInfo InventoryElementIconField =
        AccessTools.Field(AccessTools.Inner(typeof(InventoryGrid), "Element"), "m_icon");
    private static readonly FieldInfo HotbarElementsField =
        AccessTools.Field(typeof(HotkeyBar), "m_elements");
    private static readonly FieldInfo HotbarElementIconField =
        AccessTools.Field(AccessTools.Inner(typeof(HotkeyBar), "ElementData"), "m_icon");

    private static readonly ConditionalWeakTable<HotkeyBar, HotbarCache> HotbarCaches = new();

    private sealed class HotbarCache
    {
        internal readonly List<Image> Icons = new();
        internal readonly List<ItemDrop.ItemData?> Items = new();
        internal readonly List<ItemDrop.ItemData> BoundItems = new();
    }

    internal static bool HasVisibleAffinity(ItemDrop.ItemData? item)
    {
        AffinityLoadResult affinity = AffinityState.Read(item);
        return affinity == AffinityLoadResult.Lunge
            || affinity == AffinityLoadResult.Snipe
            || affinity == AffinityLoadResult.Test;
    }

    internal static void RefreshInventory(InventoryGrid grid, Inventory inventory)
    {
        IEnumerable elements = (IEnumerable)InventoryElementsField.GetValue(grid);
        foreach (object element in elements)
        {
            Vector2i position = (Vector2i)InventoryElementPositionField.GetValue(element);
            Image icon = (Image)InventoryElementIconField.GetValue(element);
            Set(icon, inventory.GetItemAt(position.x, position.y));
        }
    }

    internal static void RefreshHotbar(HotkeyBar hotbar, Player? player)
    {
        IList elements = (IList)HotbarElementsField.GetValue(hotbar);
        HotbarCache cache = HotbarCaches.GetOrCreateValue(hotbar);
        if (cache.Icons.Count != elements.Count)
        {
            cache.Icons.Clear();
            cache.Items.Clear();
            for (int index = 0; index < elements.Count; index++)
            {
                cache.Icons.Add((Image)HotbarElementIconField.GetValue(elements[index]));
                cache.Items.Add(null);
            }
        }

        for (int index = 0; index < cache.Items.Count; index++) cache.Items[index] = null;
        cache.BoundItems.Clear();
        player?.GetInventory().GetBoundItems(cache.BoundItems);
        for (int index = 0; index < cache.BoundItems.Count; index++)
        {
            ItemDrop.ItemData item = cache.BoundItems[index];
            int slot = item.m_gridPos.x;
            if (slot >= 0 && slot < cache.Items.Count) cache.Items[slot] = item;
        }
        for (int index = 0; index < cache.Icons.Count; index++)
        {
            Set(cache.Icons[index], cache.Items[index]);
        }
    }

    private static void Set(Image source, ItemDrop.ItemData? item)
    {
        Transform? existing = source.transform.parent.Find(OverlayName);
        AffinitySpritePulse? pulse = existing?.GetComponent<AffinitySpritePulse>();
        if (!HasVisibleAffinity(item))
        {
            if (pulse != null) pulse.gameObject.SetActive(false);
            return;
        }

        pulse ??= Create(source);
        pulse.Synchronize(source);
        pulse.gameObject.SetActive(true);
    }

    private static AffinitySpritePulse Create(Image source)
    {
        GameObject overlay = new(OverlayName);
        overlay.transform.SetParent(source.transform.parent, worldPositionStays: false);
        overlay.transform.SetSiblingIndex(source.transform.GetSiblingIndex() + 1);

        RectTransform sourceRect = source.rectTransform;
        RectTransform rect = overlay.AddComponent<RectTransform>();
        rect.anchorMin = sourceRect.anchorMin;
        rect.anchorMax = sourceRect.anchorMax;
        rect.pivot = sourceRect.pivot;
        rect.anchoredPosition = sourceRect.anchoredPosition;
        rect.sizeDelta = sourceRect.sizeDelta;
        rect.localRotation = sourceRect.localRotation;

        Image image = overlay.AddComponent<Image>();
        image.raycastTarget = false;
        image.maskable = source.maskable;
        image.type = source.type;
        image.preserveAspect = source.preserveAspect;
        image.fillCenter = source.fillCenter;
        image.fillMethod = source.fillMethod;
        image.fillAmount = source.fillAmount;
        image.fillClockwise = source.fillClockwise;
        image.fillOrigin = source.fillOrigin;

        AffinitySpritePulse pulse = overlay.AddComponent<AffinitySpritePulse>();
        pulse.Synchronize(source);
        return pulse;
    }
}

internal sealed class AffinitySpritePulse : MonoBehaviour
{
    private const float MinimumAlpha = 0.06f;
    private const float MaximumAlpha = 0.18f;
    private const float MinimumScale = 1.01f;
    private const float MaximumScale = 1.07f;
    private const float CyclesPerSecond = 0.55f;

    private Image source = null!;
    private Image overlay = null!;

    internal void Synchronize(Image nativeSource)
    {
        source = nativeSource;
        overlay ??= GetComponent<Image>();
        overlay.sprite = source.sprite;
        overlay.material = source.material;
    }

    private void Update()
    {
        if (source == null || overlay == null)
        {
            gameObject.SetActive(false);
            return;
        }

        overlay.enabled = source.isActiveAndEnabled && source.sprite != null;
        overlay.sprite = source.sprite;
        overlay.material = source.material;
        float phase = (Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * CyclesPerSecond) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(MinimumAlpha, MaximumAlpha, phase) * source.color.a;
        overlay.color = new Color(source.color.r, source.color.g, source.color.b, alpha);
        float scale = Mathf.Lerp(MinimumScale, MaximumScale, phase);
        transform.localScale = source.rectTransform.localScale * scale;
    }
}

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.UpdateInventory))]
internal static class AffinityInventoryIconPatch
{
    [HarmonyPostfix]
    private static void Postfix(InventoryGrid __instance, Inventory inventory)
    {
        AffinityIconAnimation.RefreshInventory(__instance, inventory);
    }
}

[HarmonyPatch(typeof(HotkeyBar), "UpdateIcons")]
internal static class AffinityHotbarIconPatch
{
    [HarmonyPostfix]
    private static void Postfix(HotkeyBar __instance, Player player)
    {
        AffinityIconAnimation.RefreshHotbar(__instance, player);
    }
}
