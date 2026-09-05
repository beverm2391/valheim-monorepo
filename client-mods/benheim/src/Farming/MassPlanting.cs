using System;
using System.Collections.Generic;
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

        if (!PlantingRules.TryGetGridSpacing(anchorPiece.gameObject, out float gridSpacing)
            || !FarmingInput.IsMassActionHeld())
        {
            return;
        }

        PlantingState.MassPlacementRunning = true;
        try
        {
            PlantGrid(player, anchorPiece, gridSpacing);
        }
        finally
        {
            PlantingState.MassPlacementRunning = false;
        }
    }

    private static void PlantGrid(Player player, Piece anchorPiece, float gridSpacing)
    {
        Heightmap? heightmap = Heightmap.FindHeightmap(PlantingState.AnchorPosition);
        if (!heightmap)
        {
            PlantingDiagnostics.PlacementFinished(PlantingState.GridSize, 0, 0, 0, "no_heightmap");
            return;
        }

        PieceTable? pieceTable = FarmingReflection.BuildPiecesField.GetValue(player) as PieceTable;
        bool freeBuild = ZoneSystem.instance.GetGlobalKey(anchorPiece.FreeBuildKey());
        List<FarmingGridPoint> points = FarmingGrid.Build(
            PlantingState.AnchorPosition,
            gridSpacing,
            PlantingState.AnchorRotation,
            PlantingState.GridSize);

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
                continue;
            }

            if (!PlantingRules.HasGrowSpace(point.Position, anchorPiece.gameObject))
            {
                blocked++;
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

        PlantingDiagnostics.PlacementFinished(PlantingState.GridSize, planted, notCultivated, blocked);
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
        PlantingDiagnostics.PlacementFinished(PlantingState.GridSize, planted, notCultivated,
            blocked, stopReason, point.Index);
    }
}
