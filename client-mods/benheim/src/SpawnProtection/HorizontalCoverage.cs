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

    // Mark angular intervals once per neighboring circle, then prefix-sum the
    // sample mask. This computes the same clipped samples in O(areas + samples)
    // per owner, rather than checking every area for every marker every frame.
    // The caller reuses the scratch array; fully contained/coincident rings
    // return immediately and never require a Unity object or terrain query.
    internal static void BuildCoverage(IReadOnlyList<Circle> circles, int owner, int[] coverage)
    {
        if (owner < 0 || owner >= circles.Count) throw new ArgumentOutOfRangeException(nameof(owner));
        Circle source = circles[owner];
        int count = SegmentCount(source);
        if (coverage.Length < count + 1) throw new ArgumentException("Coverage scratch buffer is too small.");
        Array.Clear(coverage, 0, count + 1);
        const double turn = Math.PI * 2d;
        double step = turn / count;
        for (int i = 0; i < circles.Count; i++)
        {
            if (i == owner) continue;
            Circle other = circles[i];
            double dx = (double)other.X - source.X;
            double dz = (double)other.Z - source.Z;
            double distance = Math.Sqrt(dx * dx + dz * dz);
            if (distance == 0d && source.Radius == other.Radius)
            {
                if (i > owner) continue; // Keep one stable representative.
                CoverAll();
                return;
            }
            if (distance + source.Radius <= other.Radius)
            {
                CoverAll();
                return;
            }
            if (distance > source.Radius + other.Radius || distance + other.Radius < source.Radius) continue;
            double cosine = (distance * distance + (double)source.Radius * source.Radius - (double)other.Radius * other.Radius)
                / (2d * distance * source.Radius);
            double half = Math.Acos(Math.Max(-1d, Math.Min(1d, cosine)));
            double center = Math.Atan2(dx, dz); // Sample uses (sin, cos).
            if (center < 0d) center += turn;
            double start = center - half;
            double end = center + half;
            if (start < 0d) { Mark(0d, end); Mark(start + turn, turn); }
            else if (end >= turn) { Mark(start, turn); Mark(0d, end - turn); }
            else Mark(start, end);
        }
        int depth = 0;
        for (int i = 0; i < count; i++) { depth += coverage[i]; coverage[i] = depth; }

        void CoverAll() { for (int j = 0; j < count; j++) coverage[j] = 1; }
        void Mark(double start, double end)
        {
            int first = Math.Max(0, (int)Math.Ceiling(start / step - 1e-10d));
            int last = Math.Min(count - 1, (int)Math.Floor(end / step + 1e-10d));
            if (first > last) return;
            coverage[first]++;
            coverage[last + 1]--;
        }
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
