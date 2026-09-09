using System;

namespace BenheimQoL.Affinities;

internal static class SnipeRules
{
    internal const float OpticalZoom = 3f;
    internal const float DrawDurationMultiplier = 1.25f;
    internal const float NearDistanceMeters = 20f;
    internal const float CapDistanceMeters = 60f;
    internal const float NearMultiplier = 1.25f;
    internal const float CapMultiplier = 2.25f;

    internal static float ScopedFieldOfView(float nativeFieldOfView)
    {
        // Optical magnification scales the projection plane, not the angle.
        // Dividing 65 degrees by three would magnify more than the promised 3x.
        double halfAngle = nativeFieldOfView * Math.PI / 360.0;
        return (float)(Math.Atan(Math.Tan(halfAngle) / OpticalZoom) * 360.0 / Math.PI);
    }

    internal static float DistanceMultiplier(float distanceMeters)
    {
        if (float.IsNaN(distanceMeters) || distanceMeters <= NearDistanceMeters)
        {
            return NearMultiplier;
        }

        float progress = Math.Min(1f,
            (distanceMeters - NearDistanceMeters) / (CapDistanceMeters - NearDistanceMeters));
        return NearMultiplier + (CapMultiplier - NearMultiplier) * progress;
    }

    internal static float EdgeOpacity(float horizontal, float vertical)
    {
        // A broad, fully clear center and soft independent screen edges.
        // Combining edge ramps avoids a circular rifle-scope boundary.
        float x = EdgeRamp(horizontal);
        float y = EdgeRamp(vertical);
        return x + y - x * y;
    }

    private static float EdgeRamp(float coordinate)
    {
        float t = Math.Max(0f, Math.Min(1f, (Math.Abs(coordinate) - 0.55f) / 0.45f));
        return t * t * (3f - 2f * t);
    }
}
