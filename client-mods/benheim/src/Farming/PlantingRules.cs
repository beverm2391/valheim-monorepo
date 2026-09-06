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

    internal static bool TryGetGridSpacing(GameObject prefab, out float spacing)
    {
        Plant? plant = prefab.GetComponent<Plant>();
        if (plant)
        {
            spacing = plant.m_growRadius * 2f;
            return true;
        }

        // Grid tuning is independent from collision clearance. Registration
        // still has to establish a native collider footprint before a berry
        // bush can reach planting, but all three bushes share one exact step.
        if (PlantableBerries.TryGetFootprint(prefab, out _))
        {
            spacing = FarmingSettings.BerryGridSpacing;
            return true;
        }

        spacing = 0f;
        return false;
    }

    internal static bool HasGrowSpace(Vector3 position, GameObject plantPrefab)
    {
        Plant? plant = plantPrefab.GetComponent<Plant>();
        float radius;
        if (plant)
        {
            radius = plant.m_growRadius;
        }
        else if (PlantableBerries.TryGetFootprint(plantPrefab, out float footprint))
        {
            radius = footprint * 0.5f;
        }
        else
        {
            return true;
        }

        Collider[] nearbyObjects = Physics.OverlapSphere(position, radius, PlantSpaceMask);
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
