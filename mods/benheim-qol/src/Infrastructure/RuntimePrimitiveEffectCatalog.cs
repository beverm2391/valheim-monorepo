using System;
using System.Collections.Generic;
using UnityEngine;

namespace BenheimQoL.Infrastructure;

internal static partial class RuntimePrimitiveCatalog
{
    private static void AddEffects(
        List<RuntimePrimitiveRecord> records,
        ObjectDB database)
    {
        foreach (StatusEffect effect in database.m_StatusEffects)
        {
            if (!IsNativeStatusEffect(effect))
            {
                continue;
            }

            records.Add(CreateStatusEffectRecord(
                "object_db_status_effect",
                $"status-effect:{effect.name}",
                effect));
        }

        foreach (GameObject itemPrefab in database.m_items)
        {
            if (!itemPrefab)
            {
                continue;
            }

            ItemDrop? item = itemPrefab.GetComponent<ItemDrop>();
            if (item == null)
            {
                continue;
            }

            ItemDrop.ItemData.SharedData shared = item.m_itemData.m_shared;
            AddItemStatusEffect(records, itemPrefab, shared, "consume_status_effect", shared.m_consumeStatusEffect);
            AddItemStatusEffect(records, itemPrefab, shared, "equip_status_effect", shared.m_equipStatusEffect);
            AddItemStatusEffect(records, itemPrefab, shared, "set_status_effect", shared.m_setStatusEffect);
            AddItemStatusEffect(records, itemPrefab, shared, "attack_status_effect", shared.m_attackStatusEffect);
            AddItemStatusEffect(records, itemPrefab, shared, "perfect_block_status_effect", shared.m_perfectBlockStatusEffect);
            AddItemStatusEffect(records, itemPrefab, shared, "full_adrenaline_status_effect", shared.m_fullAdrenalineSE);
        }
    }

    private static void AddItemStatusEffect(
        List<RuntimePrimitiveRecord> records,
        GameObject itemPrefab,
        ItemDrop.ItemData.SharedData shared,
        string donorKind,
        StatusEffect? effect)
    {
        if (!IsNativeStatusEffect(effect))
        {
            return;
        }

        records.Add(CreateStatusEffectRecord(
                donorKind,
                $"item:{itemPrefab.name}:{donorKind}:{effect!.name}",
                effect)
            .String("item_prefab", itemPrefab.name)
            .String("item_name_token", NullIfEmpty(shared.m_name))
            .String("item_display_name", TryLocalize(shared.m_name)));
    }

    private static RuntimePrimitiveRecord CreateStatusEffectRecord(
        string donorKind,
        string identity,
        StatusEffect effect)
    {
        return new RuntimePrimitiveRecord("effects", donorKind, identity)
            .String("internal_identity", effect.name)
            .Integer("name_hash", effect.NameHash())
            .String("display_name_token", NullIfEmpty(effect.m_name))
            .String("display_name", TryLocalize(effect.m_name))
            .Boolean("icon_present", effect.m_icon != null)
            .String("sprite_identity", StableSpriteIdentity(effect.m_icon))
            .String("runtime_type", effect.GetType().FullName);
    }

    private static bool IsNativeStatusEffect(StatusEffect? effect)
    {
        return effect != null
            && RuntimePrimitiveCatalogPolicy.IsNativeRuntimeType(
                effect.GetType().Assembly,
                typeof(StatusEffect).Assembly);
    }

    private static string? TryLocalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Localization.instance == null)
        {
            return value![0] == '$' ? null : value;
        }

        string localized = Localization.instance.Localize(value);
        return value![0] == '$' && string.Equals(localized, value, StringComparison.Ordinal)
            ? null
            : NullIfEmpty(localized);
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
