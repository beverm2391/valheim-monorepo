namespace BenheimQoL.WorldLabels;

internal static class WorldLabelVisibility
{
    internal const float PortalMaxDistanceMeters = 30f;
    internal const float PortalRefreshIntervalSeconds = 0.5f;

    private const float PortalMaxDistanceSquared =
        PortalMaxDistanceMeters * PortalMaxDistanceMeters;

    internal static bool ShouldShowPortalTag(
        string? tag,
        bool hasLocalViewer,
        float distanceSquared,
        bool hasLineOfSight)
    {
        return !string.IsNullOrEmpty(tag) &&
            hasLocalViewer &&
            distanceSquared <= PortalMaxDistanceSquared &&
            hasLineOfSight;
    }
}
