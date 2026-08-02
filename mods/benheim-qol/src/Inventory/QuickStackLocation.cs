using UnityEngine;

namespace BenheimQoL.InventoryFeature;

internal static class QuickStackLocation
{
    private static readonly string[] CompassDirections =
        { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

    internal static string Format(Player player, Container container)
    {
        Vector3 offset = container.transform.position - player.transform.position;
        float distance = new Vector2(offset.x, offset.z).magnitude;
        float heading = (Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg + 360f) % 360f;
        int directionIndex = Mathf.RoundToInt(heading / 45f) % CompassDirections.Length;
        return $"{Mathf.Max(1, Mathf.RoundToInt(distance))}m {CompassDirections[directionIndex]}";
    }
}
