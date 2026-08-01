using System.Collections.Generic;
using System.Reflection;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Repair;

internal static class BuildingRepair
{
    private const float RepairRadius = 20f;
    private const int MaxPiecesPerClick = 80;

    private static readonly List<Piece> NearbyPieces = new List<Piece>(MaxPiecesPerClick);

    private static readonly MethodInfo CheckCanRemovePieceMethod =
        AccessTools.Method(typeof(Player), "CheckCanRemovePiece");

    private static readonly MethodInfo GetBuildStaminaMethod =
        AccessTools.Method(typeof(Player), "GetBuildStamina");

    private static readonly MethodInfo FaceLookDirectionMethod =
        AccessTools.Method(typeof(Player), "FaceLookDirection");

    private static readonly FieldInfo ZanimField =
        AccessTools.Field(typeof(Character), "m_zanim") ?? AccessTools.Field(typeof(Player), "m_zanim");

    internal static void RepairNearbyPieces(Player player, ItemDrop.ItemData toolItem)
    {
        if (!player.InPlaceMode())
        {
            Diagnostics.Event("Repair", "building_repair_finished", "repaired=0 reason=not_in_place_mode");
            return;
        }

        Piece anchor = player.GetHoveringPiece();
        if (!anchor)
        {
            Diagnostics.Event("Repair", "building_repair_finished", "repaired=0 reason=no_hovered_piece");
            return;
        }

        if (!CanUseRepairToolHere(player, anchor, showWardFlash: true))
        {
            Diagnostics.Event("Repair", "building_repair_finished", "repaired=0 reason=no_anchor_access");
            return;
        }

        NearbyPieces.Clear();
        Piece.GetAllPiecesInRadius(anchor.transform.position, RepairRadius, NearbyPieces);
        NearbyPieces.Sort((left, right) =>
            Vector3.SqrMagnitude(left.transform.position - anchor.transform.position)
                .CompareTo(Vector3.SqrMagnitude(right.transform.position - anchor.transform.position)));
        Diagnostics.Event(
            "Repair",
            "building_repair_scan",
            $"anchor=\"{anchor.gameObject.name}\" radius={RepairRadius:0.#} candidates={NearbyPieces.Count}");

        int repaired = 0;
        int inaccessible = 0;
        int undamaged = 0;
        int repairFailed = 0;
        bool exhaustedResources = false;
        foreach (Piece piece in NearbyPieces)
        {
            if (repaired >= MaxPiecesPerClick)
            {
                break;
            }

            if (!piece || !PrivateArea.CheckAccess(piece.transform.position, 0f, flash: false))
            {
                inaccessible++;
                continue;
            }

            WearNTear wearNTear = piece.GetComponent<WearNTear>();
            if (!wearNTear || wearNTear.GetHealthPercentage() >= 1f)
            {
                undamaged++;
                continue;
            }

            if (!CanPayRepairCost(player, toolItem))
            {
                exhaustedResources = true;
                Hud.instance.StaminaBarEmptyFlash();
                break;
            }

            if (!wearNTear.Repair())
            {
                repairFailed++;
                continue;
            }

            repaired++;
            PayRepairCost(player, toolItem);
            piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);
        }

        if (repaired == 0)
        {
            Diagnostics.Event(
                "Repair",
                "building_repair_finished",
                $"repaired=0 inaccessible={inaccessible} undamaged={undamaged} repair_failed={repairFailed} exhausted_resources={Diagnostics.Bool(exhaustedResources)}");
            player.Message(MessageHud.MessageType.TopLeft, $"No damaged build pieces within {RepairRadius:0}m");
            return;
        }

        Diagnostics.Event(
            "Repair",
            "building_repair_finished",
            $"repaired={repaired} inaccessible={inaccessible} undamaged={undamaged} repair_failed={repairFailed} exhausted_resources={Diagnostics.Bool(exhaustedResources)}");
        PlayRepairAnimation(player, toolItem);
        player.Message(MessageHud.MessageType.TopLeft, $"Repaired {repaired} pieces");
    }

    private static bool CanUseRepairToolHere(Player player, Piece piece, bool showWardFlash)
    {
        return (bool)(CheckCanRemovePieceMethod.Invoke(player, new object[] { piece }) ?? false)
            && PrivateArea.CheckAccess(piece.transform.position, 0f, flash: showWardFlash);
    }

    private static bool CanPayRepairCost(Player player, ItemDrop.ItemData toolItem)
    {
        float staminaCost = toolItem.m_shared.m_attack.m_attackStamina;
        float eitrCost = toolItem.m_shared.m_attack.m_attackEitr;
        if (!player.HaveStamina(staminaCost) || !player.HaveEitr(eitrCost))
        {
            return false;
        }

        return !toolItem.m_shared.m_useDurability || toolItem.m_durability > 0f;
    }

    private static void PayRepairCost(Player player, ItemDrop.ItemData toolItem)
    {
        player.UseStamina(GetBuildStamina(player));
        player.UseEitr(toolItem.m_shared.m_attack.m_attackEitr);
        if (toolItem.m_shared.m_useDurability)
        {
            toolItem.m_durability -= toolItem.m_shared.m_useDurabilityDrain;
        }
    }

    private static float GetBuildStamina(Player player)
    {
        return (float)(GetBuildStaminaMethod.Invoke(player, null) ?? 0f);
    }

    private static void PlayRepairAnimation(Player player, ItemDrop.ItemData toolItem)
    {
        FaceLookDirectionMethod.Invoke(player, null);
        ZSyncAnimation? zanim = (ZSyncAnimation?)ZanimField.GetValue(player);
        zanim?.SetTrigger(toolItem.m_shared.m_attack.m_attackAnimation);
    }
}
