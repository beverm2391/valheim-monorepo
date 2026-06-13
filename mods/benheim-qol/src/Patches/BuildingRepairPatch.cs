using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Patches;

[HarmonyPatch(typeof(Player), "Repair")]
internal static class BuildingRepairPatch
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

    private static bool Prefix(Player __instance, ItemDrop.ItemData toolItem)
    {
        if (!InputState.IsShiftHeld())
        {
            return true;
        }

        try
        {
            RepairNearbyPieces(__instance, toolItem);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Mass building repair failed; falling back to vanilla repair: {ex.Message}");
            return true;
        }

        return false;
    }

    private static void RepairNearbyPieces(Player player, ItemDrop.ItemData toolItem)
    {
        if (!player.InPlaceMode())
        {
            return;
        }

        Piece anchor = player.GetHoveringPiece();
        if (!anchor || !CanUseRepairToolHere(player, anchor, showWardFlash: true))
        {
            return;
        }

        NearbyPieces.Clear();
        Piece.GetAllPiecesInRadius(anchor.transform.position, RepairRadius, NearbyPieces);
        NearbyPieces.Sort((left, right) =>
            Vector3.SqrMagnitude(left.transform.position - anchor.transform.position)
                .CompareTo(Vector3.SqrMagnitude(right.transform.position - anchor.transform.position)));

        int repaired = 0;
        foreach (Piece piece in NearbyPieces)
        {
            if (repaired >= MaxPiecesPerClick)
            {
                break;
            }

            if (!piece || !PrivateArea.CheckAccess(piece.transform.position, 0f, flash: false))
            {
                continue;
            }

            WearNTear wearNTear = piece.GetComponent<WearNTear>();
            if (!wearNTear || wearNTear.GetHealthPercentage() >= 1f)
            {
                continue;
            }

            if (!CanPayRepairCost(player, toolItem))
            {
                Hud.instance.StaminaBarEmptyFlash();
                break;
            }

            if (!wearNTear.Repair())
            {
                continue;
            }

            repaired++;
            PayRepairCost(player, toolItem);
            piece.m_placeEffect.Create(piece.transform.position, piece.transform.rotation);
        }

        if (repaired == 0)
        {
            player.Message(MessageHud.MessageType.TopLeft, anchor.m_name + " $msg_doesnotneedrepair");
            return;
        }

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
