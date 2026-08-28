using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

internal static class MassPlanting
{
    internal static void TryPlantGrid(Player player)
    {
        Piece? anchorPiece = PlantingState.AnchorPiece;
        if (PlantingState.MassPlacementRunning || !PlantingState.AnchorPlaced || !anchorPiece)
        {
            return;
        }

        Plant? plant = anchorPiece.GetComponent<Plant>();
        if (!plant || !FarmingInput.IsMassActionHeld())
        {
            return;
        }

        PlantingState.MassPlacementRunning = true;
        try
        {
            PlantGrid(player, anchorPiece, plant);
        }
        finally
        {
            PlantingState.MassPlacementRunning = false;
        }
    }

    private static void PlantGrid(Player player, Piece anchorPiece, Plant plant)
    {
        Heightmap? heightmap = Heightmap.FindHeightmap(PlantingState.AnchorPosition);
        if (!heightmap)
        {
            Diagnostics.Event("Farming", "mass_plant_finished", "planted=1 extra_planted=0 reason=no_heightmap");
            return;
        }

        PieceTable? pieceTable = FarmingReflection.BuildPiecesField.GetValue(player) as PieceTable;
        bool freeBuild = ZoneSystem.instance.GetGlobalKey(anchorPiece.FreeBuildKey());
        List<FarmingGridPoint> points = FarmingGrid.Build(
            PlantingState.AnchorPosition,
            plant,
            PlantingState.AnchorRotation);

        int planted = 0;
        int notCultivated = 0;
        int blocked = 0;
        foreach (FarmingGridPoint point in points)
        {
            if (point.IsAnchor)
            {
                continue;
            }

            if (anchorPiece.m_cultivatedGroundOnly && !heightmap.IsCultivated(point.Position))
            {
                notCultivated++;
                LogSkipped(point, PlantingInvalidReason.NotCultivated);
                continue;
            }

            if (!PlantingRules.HasGrowSpace(point.Position, anchorPiece.gameObject))
            {
                blocked++;
                LogSkipped(point, PlantingInvalidReason.BlockedGrowSpace);
                continue;
            }

            ItemDrop.ItemData? tool = FarmingReflection.GetRightItemMethod.Invoke(player, Array.Empty<object>()) as ItemDrop.ItemData;
            if (tool is null)
            {
                LogStopped(point, PlantingInvalidReason.MissingTool, planted, notCultivated, blocked);
                return;
            }

            float staminaCost = FarmingReflection.GetBuildStamina(player);
            if (!player.HaveStamina(staminaCost))
            {
                Hud.instance.StaminaBarUppgradeFlash();
                LogStopped(point, PlantingInvalidReason.InsufficientStamina, planted, notCultivated, blocked);
                return;
            }

            bool noPlacementCost = (bool)(FarmingReflection.NoPlacementCostField.GetValue(player) ?? false);
            if (!noPlacementCost && !player.HaveRequirements(anchorPiece, Player.RequirementMode.CanBuild))
            {
                LogStopped(point, PlantingInvalidReason.InsufficientResources, planted, notCultivated, blocked);
                return;
            }

            player.PlacePiece(anchorPiece, point.Position, PlantingState.AnchorRotation, doAttack: false);
            Game.instance.IncrementPlayerStat(PlayerStatType.Builds);
            if (!freeBuild)
            {
                player.ConsumeResources(anchorPiece.m_resources, 0, -1);
            }

            player.UseStamina(staminaCost);
            FarmingReflection.ApplyBuildSkill(player, pieceTable);

            planted++;
            if (tool.m_shared.m_useDurability)
            {
                tool.m_durability -= FarmingReflection.GetPlaceDurability(player, tool);
                if (tool.m_durability <= 0f)
                {
                    LogStopped(point, reason: null, planted, notCultivated, blocked, toolBroke: true);
                    return;
                }
            }
        }

        Diagnostics.Event(
            "Farming",
            "mass_plant_finished",
            $"planted={planted + 1} extra_planted={planted} skipped_not_cultivated={notCultivated} skipped_blocked={blocked} grid={FarmingSettings.GridWidth}x{FarmingSettings.GridLength}");
    }

    private static void LogSkipped(FarmingGridPoint point, PlantingInvalidReason reason)
    {
        Diagnostics.Event(
            "Farming",
            "plant_position_skipped",
            $"index={point.Index} row={point.Row} column={point.Column} reason={PlantingRules.Name(reason)}");
    }

    private static void LogStopped(
        FarmingGridPoint point,
        PlantingInvalidReason? reason,
        int planted,
        int notCultivated,
        int blocked,
        bool toolBroke = false)
    {
        string stopReason = toolBroke ? "tool_broke" : PlantingRules.Name(reason ?? PlantingInvalidReason.None);
        Diagnostics.Event(
            "Farming",
            "plant_position_stopped",
            $"index={point.Index} row={point.Row} column={point.Column} reason={stopReason}");
        Diagnostics.Event(
            "Farming",
            "mass_plant_finished",
            $"planted={planted + 1} extra_planted={planted} skipped_not_cultivated={notCultivated} skipped_blocked={blocked} stopped={stopReason}");
    }
}
