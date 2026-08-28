using BenheimQoL.Infrastructure;

namespace BenheimQoL.Interaction;

internal readonly struct TarCollectibleInteractionObservation
{
    internal TarCollectibleInteractionObservation(
        bool report,
        string form,
        string prefab,
        bool submergedInTar,
        bool exemptionApplied)
    {
        Report = report;
        Form = form;
        Prefab = prefab;
        SubmergedInTar = submergedInTar;
        ExemptionApplied = exemptionApplied;
    }

    internal bool Report { get; }
    internal string Form { get; }
    internal string Prefab { get; }
    internal bool SubmergedInTar { get; }
    internal bool ExemptionApplied { get; }
}

/// <summary>
/// Identifies only Valheim's native Tar collection forms and removes only the
/// two manual-interaction tar gates. The installed 0.221.12 assets contain a
/// gated Pickable_Tar, an already-ungated Pickable_TarBig, and the Tar
/// ItemDrop spawned by both. Player.AutoPickup keeps its separate InTar gate.
/// </summary>
internal static class TarCollectibleInteraction
{
    internal const string TarItemPrefab = "Tar";
    internal const string TarItemName = "$item_tar";
    internal const string SmallTarPickablePrefab = "Pickable_Tar";
    internal const string BigTarPickablePrefab = "Pickable_TarBig";

    internal static bool ShouldBlockPickable(Pickable pickable)
    {
        return pickable.m_tarPreventsPicking && !IsNativeTarPickable(pickable, out _);
    }

    internal static bool ShouldBlockItemDrop(ItemDrop itemDrop)
    {
        return itemDrop.InTar() && !IsNativeTarItemDrop(itemDrop, out _);
    }

    internal static TarCollectibleInteractionObservation Observe(Pickable pickable)
    {
        if (!IsNativeTarPickable(pickable, out string prefab))
        {
            return default;
        }

        Floating? floating = pickable.GetComponent<Floating>();
        bool submergedInTar = floating != null && floating.IsInTar();
        return new TarCollectibleInteractionObservation(
            report: true,
            form: "pickable",
            prefab,
            submergedInTar,
            exemptionApplied: pickable.m_tarPreventsPicking && submergedInTar);
    }

    internal static TarCollectibleInteractionObservation Observe(ItemDrop itemDrop)
    {
        if (!IsNativeTarItemDrop(itemDrop, out string prefab))
        {
            return default;
        }

        bool submergedInTar = itemDrop.InTar();
        return new TarCollectibleInteractionObservation(
            report: true,
            form: "item_drop",
            prefab,
            submergedInTar,
            exemptionApplied: submergedInTar);
    }

    internal static void ReportResult(
        TarCollectibleInteractionObservation observation,
        bool nativeResult)
    {
        if (!observation.Report)
        {
            return;
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("Interaction", "tar_collectible_interaction")
                .String("collectible_form", observation.Form)
                .String("prefab", observation.Prefab)
                .Boolean("submerged_in_tar", observation.SubmergedInTar)
                .Boolean("exemption_applied", observation.ExemptionApplied)
                .Boolean("native_result", nativeResult));
    }

    private static bool IsNativeTarPickable(Pickable pickable, out string prefab)
    {
        prefab = Utils.GetPrefabName(pickable.gameObject);
        if (prefab != SmallTarPickablePrefab && prefab != BigTarPickablePrefab)
        {
            return false;
        }

        if (pickable.m_itemPrefab == null
            || Utils.GetPrefabName(pickable.m_itemPrefab) != TarItemPrefab)
        {
            return false;
        }

        ItemDrop? itemDrop = pickable.m_itemPrefab.GetComponent<ItemDrop>();
        return itemDrop != null && HasNativeTarItemData(itemDrop.m_itemData);
    }

    private static bool IsNativeTarItemDrop(ItemDrop itemDrop, out string prefab)
    {
        prefab = Utils.GetPrefabName(itemDrop.gameObject);
        return prefab == TarItemPrefab
            && itemDrop.m_itemData.m_dropPrefab != null
            && Utils.GetPrefabName(itemDrop.m_itemData.m_dropPrefab) == TarItemPrefab
            && HasNativeTarItemData(itemDrop.m_itemData);
    }

    private static bool HasNativeTarItemData(ItemDrop.ItemData itemData)
    {
        return itemData.m_shared.m_name == TarItemName
            && itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material;
    }
}
