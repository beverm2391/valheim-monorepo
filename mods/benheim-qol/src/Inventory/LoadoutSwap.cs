using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.InventoryFeature;

/// <summary>
/// Replaces the default R hand-item hide only when Ben's two explicitly placed
/// hotbar loadouts can both exist. All equipment state changes stay in
/// Humanoid's native equip methods, so its normal combat, swimming, broken-item,
/// DLC, and visual-equipment rules remain authoritative.
/// </summary>
internal static class LoadoutSwap
{
    private const int PairFirstColumn = 0;
    private const int PairSecondColumn = 1;
    private const int SoloColumn = 2;

    internal static bool TryHandlePlainR(Player? player)
    {
        if (player == null || InputState.IsTextEntryActive())
        {
            return false;
        }

        ItemDrop.ItemData? first = player.GetInventory().GetItemAt(PairFirstColumn, 0);
        ItemDrop.ItemData? second = player.GetInventory().GetItemAt(PairSecondColumn, 0);
        ItemDrop.ItemData? solo = player.GetInventory().GetItemAt(SoloColumn, 0);
        if (!CanFormLoadouts(player, first, second, solo, out string rejection))
        {
            Diagnostics.Event("Loadout", "swap_rejected", $"reason={rejection}");
            return false;
        }

        bool pairIsEquipped = player.IsItemEquiped(first!) && player.IsItemEquiped(second!);
        bool swapped = pairIsEquipped
            ? EquipSolo(player, first!, second!, solo!)
            : EquipPair(player, first!, second!, solo!);
        if (!swapped)
        {
            // This should be unreachable after CanFormLoadouts. Preserve native R
            // if a future Valheim restriction makes the native action decline.
            Diagnostics.Event("Loadout", "swap_rejected", "reason=native_equip_declined");
            return false;
        }

        Diagnostics.Event(
            "Loadout",
            "swapped",
            $"target={(pairIsEquipped ? "solo" : "pair")} first={ItemName(first!)} second={ItemName(second!)} solo={ItemName(solo!)}");
        return true;
    }

    private static bool CanFormLoadouts(
        Player player,
        ItemDrop.ItemData? first,
        ItemDrop.ItemData? second,
        ItemDrop.ItemData? solo,
        out string rejection)
    {
        if (first == null || second == null || solo == null)
        {
            rejection = "missing_hotbar_item";
            return false;
        }

        if (!CanUseNativeEquip(player, first)
            || !CanUseNativeEquip(player, second)
            || !CanUseNativeEquip(player, solo))
        {
            rejection = "native_equip_restriction";
            return false;
        }

        if (!CanEquipTogether(first, second))
        {
            rejection = "pair_incompatible";
            return false;
        }

        rejection = string.Empty;
        return true;
    }

    private static bool CanUseNativeEquip(Player player, ItemDrop.ItemData item)
    {
        if (!item.IsEquipable()
            || player.InAttack()
            || player.InDodge()
            || player.IsDead()
            || (player.IsSwimming() && !player.IsOnGround())
            || (item.m_shared.m_useDurability && item.m_durability <= 0f))
        {
            return false;
        }

        if (item.m_shared.m_dlc.Length > 0
            && (DLCMan.instance == null || !DLCMan.instance.IsDLCInstalled(item.m_shared.m_dlc)))
        {
            return false;
        }

        return Game.m_worldLevel <= 0
            || item.m_worldLevel >= Game.m_worldLevel
            || (item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Utility
                && item.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Trinket);
    }

    private static bool CanEquipTogether(ItemDrop.ItemData first, ItemDrop.ItemData second)
    {
        EquipmentSlot firstSlot = GetEquipmentSlot(first);
        EquipmentSlot secondSlot = GetEquipmentSlot(second);
        if (IsHandSlot(firstSlot) && IsHandSlot(secondSlot))
        {
            return IsCompatibleHandPair(first.m_shared.m_itemType, second.m_shared.m_itemType);
        }

        return firstSlot != EquipmentSlot.None && firstSlot != secondSlot;
    }

    private static bool IsHandSlot(EquipmentSlot slot)
    {
        return slot == EquipmentSlot.RightHand || slot == EquipmentSlot.LeftHand;
    }

    private static bool IsCompatibleHandPair(
        ItemDrop.ItemData.ItemType first,
        ItemDrop.ItemData.ItemType second)
    {
        return (IsOneHanded(first) && IsOffHand(second))
            || (IsOneHanded(second) && IsOffHand(first))
            || (first == ItemDrop.ItemData.ItemType.Torch
                && second == ItemDrop.ItemData.ItemType.Shield)
            || (first == ItemDrop.ItemData.ItemType.Shield
                && second == ItemDrop.ItemData.ItemType.Torch);
    }

    private static bool IsOneHanded(ItemDrop.ItemData.ItemType itemType)
    {
        return itemType == ItemDrop.ItemData.ItemType.OneHandedWeapon;
    }

    private static bool IsOffHand(ItemDrop.ItemData.ItemType itemType)
    {
        return itemType == ItemDrop.ItemData.ItemType.Shield
            || itemType == ItemDrop.ItemData.ItemType.Torch;
    }

    private static EquipmentSlot GetEquipmentSlot(ItemDrop.ItemData item)
    {
        switch (item.m_shared.m_itemType)
        {
            case ItemDrop.ItemData.ItemType.Tool:
            case ItemDrop.ItemData.ItemType.OneHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
                return EquipmentSlot.RightHand;
            case ItemDrop.ItemData.ItemType.Torch:
            case ItemDrop.ItemData.ItemType.Shield:
            case ItemDrop.ItemData.ItemType.Bow:
            case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
                return EquipmentSlot.LeftHand;
            case ItemDrop.ItemData.ItemType.Chest:
                return EquipmentSlot.Chest;
            case ItemDrop.ItemData.ItemType.Legs:
                return EquipmentSlot.Legs;
            case ItemDrop.ItemData.ItemType.Helmet:
                return EquipmentSlot.Helmet;
            case ItemDrop.ItemData.ItemType.Shoulder:
                return EquipmentSlot.Shoulder;
            case ItemDrop.ItemData.ItemType.Ammo:
            case ItemDrop.ItemData.ItemType.AmmoNonEquipable:
                return EquipmentSlot.Ammo;
            case ItemDrop.ItemData.ItemType.Utility:
                return EquipmentSlot.Utility;
            case ItemDrop.ItemData.ItemType.Trinket:
                return EquipmentSlot.Trinket;
            default:
                return EquipmentSlot.None;
        }
    }

    private static bool EquipPair(
        Player player,
        ItemDrop.ItemData first,
        ItemDrop.ItemData second,
        ItemDrop.ItemData solo)
    {
        player.UnequipItem(solo);
        bool firstEquipped = player.IsItemEquiped(first) || player.EquipItem(first);
        bool secondEquipped = player.IsItemEquiped(second) || player.EquipItem(second);
        return firstEquipped
            && secondEquipped
            && player.IsItemEquiped(first)
            && player.IsItemEquiped(second);
    }

    private static bool EquipSolo(
        Player player,
        ItemDrop.ItemData first,
        ItemDrop.ItemData second,
        ItemDrop.ItemData solo)
    {
        player.UnequipItem(first);
        player.UnequipItem(second);
        return player.IsItemEquiped(solo) || player.EquipItem(solo);
    }

    private static string ItemName(ItemDrop.ItemData item)
    {
        return item.m_shared.m_name.Replace(' ', '_');
    }

    private enum EquipmentSlot
    {
        None,
        RightHand,
        LeftHand,
        Chest,
        Legs,
        Helmet,
        Shoulder,
        Ammo,
        Utility,
        Trinket,
    }
}

/// <summary>
/// Player.Update asks the native Hide binding only after Player.TakeInput has
/// approved gameplay input. Intercepting that exact query preserves every native
/// blocking UI gate and lets ordinary R pass through when no loadout exists.
/// </summary>
[HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown), new[] { typeof(string) })]
internal static class LoadoutSwapInputPatch
{
    private static bool Prefix(string name, ref bool __result)
    {
        if (name != "Hide"
            || InputState.IsModifierHeld()
            || !InputState.IsKeyDown(KeyCode.R))
        {
            return true;
        }

        if (!LoadoutSwap.TryHandlePlainR(Player.m_localPlayer))
        {
            return true;
        }

        __result = false;
        return false;
    }
}
