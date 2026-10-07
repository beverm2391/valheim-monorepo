using System;
using System.Diagnostics;
using System.Collections.Generic;
using BenheimQoL.SpawnProtection;
using Circle = BenheimQoL.SpawnProtection.HorizontalCoverage.Circle;

internal static class Program
{
    private static void Main()
    {
        Circle a = new(0f, 0f, 20f);
        int count = HorizontalCoverage.SegmentCount(a);
        Check(count == 80, "native-sized circle uses ESP sampling density");
        Circle[] alone = { a };
        for (int i = 0; i < count; i++)
        {
            HorizontalCoverage.Sample(a, i, count, 0.31f, out float x, out float z);
            Check(Math.Abs(Math.Sqrt(x * x + z * z) - 20d) < 0.00001d, "sample stays on boundary");
            Check(!IsCovered(alone, 0, x, z), "an isolated ring stays visible");
        }

        Circle[] overlapping = { a, new(20f, 0f, 20f) };
        Check(IsCovered(overlapping, 0, 20f, 0f), "interior arc is hidden");
        Check(!IsCovered(overlapping, 0, -20f, 0f), "outer arc stays visible");
        Check(!IsCovered(overlapping, 1, 40f, 0f), "neighbor's outer arc stays visible");

        Circle[] nested = { a, new(0f, 0f, 10f) };
        Circle[] mixed = { a, new(15f, 0f, 8f) };
        Check(IsCovered(mixed, 0, 20f, 0f), "small neighbor covers part of large ring");
        Check(!IsCovered(mixed, 0, -20f, 0f), "large outer arc stays visible with mixed radii");
        Check(!IsCovered(mixed, 1, 23f, 0f), "small outer arc extends beyond large ring");
        Check(IsCovered(mixed, 1, 7f, 0f), "small interior arc hides inside large circle");
        Check(IsCovered(nested, 1, 10f, 0f), "contained ring is hidden");
        Check(!IsCovered(nested, 0, 20f, 0f), "containing ring stays visible");
        Circle[] separated = { a, new(50f, 0f, 20f) };
        Check(!IsCovered(separated, 0, 20f, 0f), "gap keeps both edges visible");

        Circle[] identical = { a, a, a };
        for (int i = 0; i < count; i++)
        {
            HorizontalCoverage.Sample(a, i, count, 0.17f, out float x, out float z);
            Check(!IsCovered(identical, 0, x, z), "first coincident ring remains visible");
            Check(IsCovered(identical, 1, x, z), "second coincident ring is hidden");
            Check(IsCovered(identical, 2, x, z), "third coincident ring is hidden");
        }

        Circle shifted = new(100f, -80f, 20f);
        HorizontalCoverage.Sample(shifted, 0, 80, 0f, out float sx, out float sz);
        Check(sx == 100f && sz == -60f, "world center offsets boundary");
        Check(HorizontalCoverage.SegmentCount(new Circle(0f, 0f, 0.1f)) == 3, "small circle remains drawable");
        Reject(() => new Circle(0f, 0f, 0f));
        Reject(() => new Circle(0f, 0f, float.NaN));
        Reject(() => new Circle(float.PositiveInfinity, 0f, 20f));
        Reject(() => IsCovered(alone, 1, 0f, 0f));
        Reject(() => HorizontalCoverage.Sample(a, 80, 80, 0f, out _, out _));
        VerifyMasks(overlapping);
        VerifyMasks(nested);
        VerifyMasks(mixed);
        VerifyMasks(separated);
        VerifyMasks(identical);
        VerifyMasks(new[] { a, new Circle(40f, 0f, 20f) }); // External tangency.
        VerifyMasks(new[] { a, new Circle(10f, 0f, 10f) }); // Internal tangency.
        Random random = new(734);
        for (int pass = 0; pass < 50; pass++)
        {
            Circle[] varied = new Circle[30];
            for (int i = 0; i < varied.Length; i++)
                varied[i] = new Circle((float)random.NextDouble() * 100f - 50f,
                    (float)random.NextDouble() * 100f - 50f, (float)random.NextDouble() * 25f + 1f);
            VerifyMasks(varied);
        }
        DenseProof(215);
        DenseProof(1000);
        Console.WriteLine("Spawn protection horizontal coverage checks passed");
    }

    private static void VerifyMasks(Circle[] circles)
    {
        for (int owner = 0; owner < circles.Length; owner++)
        {
            int count = HorizontalCoverage.SegmentCount(circles[owner]);
            int[] mask = new int[count + 1];
            HorizontalCoverage.BuildCoverage(circles, owner, mask);
            for (int i = 0; i < count; i++)
            {
                HorizontalCoverage.Sample(circles[owner], i, count, 0f, out float x, out float z);
                bool expected = IsCovered(circles, owner, x, z);
                // Float sample coordinates can land on either side of exact
                // tangency. Compare away from that sub-millimeter boundary.
                bool nearEdge = false;
                for (int j = 0; j < circles.Length; j++)
                {
                    if (j == owner || Same(circles[owner], circles[j])) continue;
                    double dx = (double)x - circles[j].X, dz = (double)z - circles[j].Z;
                    if (Math.Abs(Math.Sqrt(dx * dx + dz * dz) - circles[j].Radius) < 0.00001d) nearEdge = true;
                }
                if (!nearEdge) Check((mask[i] != 0) == expected, "angular mask matches point-in-circle reference");
            }
        }
    }

    private static bool Same(Circle a, Circle b) => a.X == b.X && a.Z == b.Z && a.Radius == b.Radius;

    private static void DenseProof(int areas)
    {
        Circle[] dense = new Circle[areas];
        for (int i = 0; i < areas; i++) dense[i] = new Circle(i % 25 * 2f, i / 25 * 2f, 20f);
        int[] mask = new int[81];
        int visible = 0;
        Stopwatch clock = Stopwatch.StartNew();
        for (int i = 0; i < areas; i++)
        {
            HorizontalCoverage.BuildCoverage(dense, i, mask);
            for (int j = 0; j < 80; j++) if (mask[j] == 0) visible++;
        }
        clock.Stop();
        Check(visible > 0 && visible < areas * 80 / 10, "dense base keeps exterior while pruning hidden marker work");
        Console.WriteLine($"Dense geometry: areas={areas} old_markers={areas * 80} visible_markers={visible} build_ms={clock.Elapsed.TotalMilliseconds:F2}");
        VerifyMasks(dense);
    }

    private static bool IsCovered(IReadOnlyList<Circle> circles, int owner, float x, float z)
    {
        if (owner < 0 || owner >= circles.Count)
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

    private static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
    }

    private static void Reject(Action action)
    {
        try { action(); }
        catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("invalid geometry should be rejected");
    }
}
