using UnityEngine;

namespace BenheimQoL.Farming;

internal enum PlantingInvalidReason
{
    None,
    Anchor,
    NoHeightmap,
    NotCultivated,
    BlockedGrowSpace,
    MissingTool,
    InsufficientStamina,
    MissingRequirement,
    InsufficientResources,
}

internal static class PlantingRules
{
    private static readonly int PlantSpaceMask =
        LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "piece_nonsolid");

    internal static bool HasGrowSpace(Vector3 position, GameObject plantPrefab)
    {
        Plant? plant = plantPrefab.GetComponent<Plant>();
        if (!plant)
        {
            return true;
        }

        Collider[] nearbyObjects = Physics.OverlapSphere(position, plant.m_growRadius, PlantSpaceMask);
        return nearbyObjects.Length == 0;
    }

    internal static string Name(PlantingInvalidReason reason)
    {
        return reason switch
        {
            PlantingInvalidReason.Anchor => "anchor",
            PlantingInvalidReason.NoHeightmap => "no_heightmap",
            PlantingInvalidReason.NotCultivated => "not_cultivated",
            PlantingInvalidReason.BlockedGrowSpace => "blocked_grow_space",
            PlantingInvalidReason.MissingTool => "missing_tool",
            PlantingInvalidReason.InsufficientStamina => "insufficient_stamina",
            PlantingInvalidReason.MissingRequirement => "missing_requirement",
            PlantingInvalidReason.InsufficientResources => "insufficient_resources",
            _ => "valid",
        };
    }
}
