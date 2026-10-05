using System;
using System.Collections.Generic;

namespace BenheimQoL.SpawnProtection;

// Adapted from ESP 1.40's CircleAreaManager / CircleRuler (Unlicense):
// https://github.com/JereKuusela/valheim-esp/tree/89b30a31f4904057c09abf03c75fe80f4fbc8323/ESP/Visualization
// This is a horizontal preview, independent of terrain height and the native
// collider's vertical extent. The renderer supplies loaded PlayerBase circles.
internal static class HorizontalCoverage
{
    internal readonly struct Circle
    {
        internal Circle(float x, float z, float radius)
        {
            if (!IsFinite(x) || !IsFinite(z) || !IsFinite(radius) || radius <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(radius), "Coverage needs a finite center and positive finite radius.");
            }

            X = x;
            Z = z;
            Radius = radius;
        }

        internal float X { get; }
        internal float Z { get; }
        internal float Radius { get; }
    }

    // Match ESP's four samples per meter of radius. Samples are positions of
    // native marker segments; they approximate the union rather than clipping
    // arbitrary polygons or claiming exact three-dimensional suppression.
    internal static int SegmentCount(Circle circle) => Math.Max(3, (int)(circle.Radius * 4f));

    internal static void Sample(Circle circle, int segment, int count, float phase, out float x, out float z)
    {
        if (count < 3 || segment < 0 || segment >= count || !IsFinite(phase))
        {
            throw new ArgumentOutOfRangeException(nameof(segment));
        }

        double angle = segment * (Math.PI * 2d / count) + phase;
        x = circle.X + (float)Math.Sin(angle) * circle.Radius;
        z = circle.Z + (float)Math.Cos(angle) * circle.Radius;
    }

    internal static bool IsCovered(IReadOnlyList<Circle> circles, int owner, float x, float z)
    {
        if (owner < 0 || owner >= circles.Count || !IsFinite(x) || !IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(nameof(owner));
        }

        Circle source = circles[owner];
        for (int i = 0; i < circles.Count; i++)
        {
            if (i == owner)
            {
                continue;
            }

            Circle other = circles[i];
            // ESP hides both boundaries when two circles are identical. Keep
            // the first representative, including elevated pieces with the
            // same horizontal footprint. Ordering must be stable per refresh.
            if (source.X == other.X && source.Z == other.Z && source.Radius == other.Radius)
            {
                if (i < owner)
                {
                    return true;
                }
                continue;
            }

            double dx = (double)x - other.X;
            double dz = (double)z - other.Z;
            if (dx * dx + dz * dz <= (double)other.Radius * other.Radius)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
