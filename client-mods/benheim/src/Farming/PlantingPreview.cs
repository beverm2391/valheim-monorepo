using System;
using System.Collections.Generic;
using System.Linq;
using BenheimQoL.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace BenheimQoL.Farming;

internal static class PlantingPreview
{
    private static GameObject[] placementGhosts = Array.Empty<GameObject>();
    private static Piece? fakeResourcePiece;
    private static string selectedPrefabName = string.Empty;
    private static string lastDiagnosticSignature = string.Empty;

    internal static void Update(Player player)
    {
        GameObject? anchorGhost = (GameObject?)FarmingReflection.PlacementGhostField.GetValue(player);
        if (!anchorGhost || !anchorGhost.activeSelf || !FarmingInput.IsMassActionHeld())
        {
            HideGhosts();
            return;
        }

        Piece? anchorPiece = anchorGhost.GetComponent<Piece>();
        if (!anchorPiece
            || !PlantingRules.TryGetGridSpacing(anchorGhost, out float gridSpacing)
            || !EnsureGhostsBuilt(player))
        {
            HideGhosts();
            return;
        }

        Piece.Requirement? requirement = anchorPiece.m_resources
            .FirstOrDefault(candidate => candidate.m_resItem && candidate.m_amount > 0);
        ItemDrop.ItemData? tool = FarmingReflection.GetRightItemMethod.Invoke(player, Array.Empty<object>()) as ItemDrop.ItemData;
        Heightmap? heightmap = Heightmap.FindHeightmap(anchorGhost.transform.position);
        List<FarmingGridPoint> points = FarmingGrid.Build(
            anchorGhost.transform.position,
            gridSpacing,
            anchorGhost.transform.rotation,
            FarmingGridSelection.CurrentSize);

        PrepareFakeRequirement(requirement);
        List<PlantingInvalidReason> reasons = EvaluateAndDraw(
            player,
            anchorGhost,
            anchorPiece,
            requirement,
            tool,
            heightmap,
            points);
        LogChangedDiagnostics(reasons);
    }

    internal static void DestroyGhosts()
    {
        foreach (GameObject ghost in placementGhosts)
        {
            if (ghost)
            {
                UnityEngine.Object.Destroy(ghost);
            }
        }

        placementGhosts = Array.Empty<GameObject>();
        fakeResourcePiece = null;
        selectedPrefabName = string.Empty;
        lastDiagnosticSignature = string.Empty;
    }

    private static List<PlantingInvalidReason> EvaluateAndDraw(
        Player player,
        GameObject anchorGhost,
        Piece anchorPiece,
        Piece.Requirement? requirement,
        ItemDrop.ItemData? tool,
        Heightmap? heightmap,
        List<FarmingGridPoint> points)
    {
        var reasons = new List<PlantingInvalidReason>(points.Count);
        float staminaCost = tool is null ? 0f : FarmingReflection.GetBuildStamina(player);
        float remainingStamina = player.GetStamina() - staminaCost;
        int cumulativeResourceAmount = requirement?.m_amount ?? 0;
        bool noPlacementCost = (bool)(FarmingReflection.NoPlacementCostField.GetValue(player) ?? false);
        bool freeBuild = ZoneSystem.instance.GetGlobalKey(anchorPiece.FreeBuildKey());

        for (int index = 0; index < points.Count; index++)
        {
            FarmingGridPoint point = points[index];
            GameObject previewGhost = placementGhosts[index];
            if (point.IsAnchor)
            {
                previewGhost.SetActive(false);
                reasons.Add(PlantingInvalidReason.Anchor);
                continue;
            }

            previewGhost.transform.position = point.Position;
            previewGhost.transform.rotation = anchorGhost.transform.rotation;
            previewGhost.SetActive(true);

            PlantingInvalidReason reason = PhysicalReason(anchorPiece, heightmap, point.Position);
            if (reason == PlantingInvalidReason.None && tool is null)
            {
                reason = PlantingInvalidReason.MissingTool;
            }

            if (reason == PlantingInvalidReason.None && requirement is null)
            {
                reason = PlantingInvalidReason.MissingRequirement;
            }

            if (reason == PlantingInvalidReason.None && remainingStamina < staminaCost)
            {
                reason = PlantingInvalidReason.InsufficientStamina;
            }

            if (reason == PlantingInvalidReason.None)
            {
                cumulativeResourceAmount += requirement!.m_amount;
                fakeResourcePiece!.m_resources[0].m_amount = cumulativeResourceAmount;
                if (!noPlacementCost
                    && !freeBuild
                    && !player.HaveRequirements(fakeResourcePiece, Player.RequirementMode.CanBuild))
                {
                    reason = PlantingInvalidReason.InsufficientResources;
                }
            }

            if (reason == PlantingInvalidReason.None)
            {
                remainingStamina -= staminaCost;
            }

            previewGhost.GetComponent<Piece>().SetInvalidPlacementHeightlight(
                reason != PlantingInvalidReason.None);
            reasons.Add(reason);
        }

        return reasons;
    }

    private static PlantingInvalidReason PhysicalReason(
        Piece anchorPiece,
        Heightmap? heightmap,
        Vector3 position)
    {
        if (!heightmap)
        {
            return PlantingInvalidReason.NoHeightmap;
        }

        if (anchorPiece.m_cultivatedGroundOnly && !heightmap.IsCultivated(position))
        {
            return PlantingInvalidReason.NotCultivated;
        }

        return PlantingRules.HasGrowSpace(position, anchorPiece.gameObject)
            ? PlantingInvalidReason.None
            : PlantingInvalidReason.BlockedGrowSpace;
    }

    private static void PrepareFakeRequirement(Piece.Requirement? requirement)
    {
        if (!fakeResourcePiece)
        {
            return;
        }

        fakeResourcePiece.m_resources[0].m_resItem = requirement?.m_resItem;
        fakeResourcePiece.m_resources[0].m_amount = requirement?.m_amount ?? 0;
    }

    private static bool EnsureGhostsBuilt(Player player)
    {
        PieceTable? pieceTable = FarmingReflection.BuildPiecesField.GetValue(player) as PieceTable;
        GameObject? prefab = pieceTable?.GetSelectedPrefab();
        if (!prefab || prefab.GetComponent<Piece>().m_repairPiece)
        {
            return false;
        }

        int gridSize = FarmingGridSelection.CurrentSize;
        int requiredSize = gridSize * gridSize;
        bool needsRebuild = placementGhosts.Length != requiredSize
            || placementGhosts.Length == 0
            || !placementGhosts[0]
            || selectedPrefabName != prefab.name;
        if (!needsRebuild)
        {
            return true;
        }

        DestroyGhosts();
        placementGhosts = new GameObject[requiredSize];
        selectedPrefabName = prefab.name;
        for (int index = 0; index < placementGhosts.Length; index++)
        {
            placementGhosts[index] = CreateGhost(prefab);
        }

        fakeResourcePiece = placementGhosts[0].GetComponent<Piece>();
        fakeResourcePiece.m_dlc = string.Empty;
        fakeResourcePiece.m_resources = new[] { new Piece.Requirement() };
        return true;
    }

    private static GameObject CreateGhost(GameObject prefab)
    {
        bool previousForceDisableInit = ZNetView.m_forceDisableInit;
        GameObject ghost;
        try
        {
            ZNetView.m_forceDisableInit = true;
            ghost = UnityEngine.Object.Instantiate(prefab);
        }
        finally
        {
            ZNetView.m_forceDisableInit = previousForceDisableInit;
        }

        ghost.name = prefab.name;
        foreach (Joint joint in ghost.GetComponentsInChildren<Joint>())
        {
            UnityEngine.Object.Destroy(joint);
        }

        foreach (Rigidbody rigidbody in ghost.GetComponentsInChildren<Rigidbody>())
        {
            UnityEngine.Object.Destroy(rigidbody);
        }

        int ghostLayer = LayerMask.NameToLayer("ghost");
        foreach (Transform child in ghost.GetComponentsInChildren<Transform>())
        {
            child.gameObject.layer = ghostLayer;
        }

        foreach (TerrainModifier modifier in ghost.GetComponentsInChildren<TerrainModifier>())
        {
            UnityEngine.Object.Destroy(modifier);
        }

        foreach (GuidePoint guidePoint in ghost.GetComponentsInChildren<GuidePoint>())
        {
            UnityEngine.Object.Destroy(guidePoint);
        }

        foreach (Light light in ghost.GetComponentsInChildren<Light>())
        {
            UnityEngine.Object.Destroy(light);
        }

        Transform ghostOnly = ghost.transform.Find("_GhostOnly");
        if (ghostOnly)
        {
            ghostOnly.gameObject.SetActive(true);
        }

        foreach (MeshRenderer renderer in ghost.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                if (!materials[index])
                {
                    continue;
                }

                var material = new Material(materials[index]);
                material.SetFloat("_RippleDistance", 0f);
                material.SetFloat("_ValueNoise", 0f);
                materials[index] = material;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        return ghost;
    }

    private static void HideGhosts()
    {
        foreach (GameObject ghost in placementGhosts)
        {
            if (ghost)
            {
                ghost.SetActive(false);
            }
        }

        lastDiagnosticSignature = string.Empty;
    }

    private static void LogChangedDiagnostics(List<PlantingInvalidReason> reasons)
    {
        string signature = string.Join(",", reasons.Select(reason => ((int)reason).ToString()));
        if (signature == lastDiagnosticSignature)
        {
            return;
        }

        lastDiagnosticSignature = signature;
        int valid = 0;
        int invalid = 0;
        int gridSize = FarmingGridSelection.CurrentSize;
        for (int index = 0; index < reasons.Count; index++)
        {
            PlantingInvalidReason reason = reasons[index];
            if (reason == PlantingInvalidReason.None)
            {
                valid++;
                continue;
            }

            if (reason == PlantingInvalidReason.Anchor)
            {
                continue;
            }

            invalid++;
            int row = index / gridSize;
            int column = index % gridSize;
            Diagnostics.Event(
                "Farming",
                "plant_preview_invalid",
                $"index={index} row={row} column={column} reason={PlantingRules.Name(reason)}");
        }

        Diagnostics.Event(
            "Farming",
            "plant_preview_updated",
            $"valid={valid} invalid={invalid} grid={gridSize}x{gridSize}");
    }
}
