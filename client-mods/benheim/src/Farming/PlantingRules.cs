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

        // Spread the grid without enlarging collision clearance around each bush.
        bool berry = PlantableBerries.TryGetFootprint(prefab, out spacing);
        spacing *= 2f;
        return berry;
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
