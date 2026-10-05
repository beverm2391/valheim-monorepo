using System;
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
            Check(!HorizontalCoverage.IsCovered(alone, 0, x, z), "an isolated ring stays visible");
        }

        Circle[] overlapping = { a, new(20f, 0f, 20f) };
        Check(HorizontalCoverage.IsCovered(overlapping, 0, 20f, 0f), "interior arc is hidden");
        Check(!HorizontalCoverage.IsCovered(overlapping, 0, -20f, 0f), "outer arc stays visible");
        Check(!HorizontalCoverage.IsCovered(overlapping, 1, 40f, 0f), "neighbor's outer arc stays visible");

        Circle[] nested = { a, new(0f, 0f, 10f) };
        Circle[] mixed = { a, new(15f, 0f, 8f) };
        Check(HorizontalCoverage.IsCovered(mixed, 0, 20f, 0f), "small neighbor covers part of large ring");
        Check(!HorizontalCoverage.IsCovered(mixed, 0, -20f, 0f), "large outer arc stays visible with mixed radii");
        Check(!HorizontalCoverage.IsCovered(mixed, 1, 23f, 0f), "small outer arc extends beyond large ring");
        Check(HorizontalCoverage.IsCovered(mixed, 1, 7f, 0f), "small interior arc hides inside large circle");
        Check(HorizontalCoverage.IsCovered(nested, 1, 10f, 0f), "contained ring is hidden");
        Check(!HorizontalCoverage.IsCovered(nested, 0, 20f, 0f), "containing ring stays visible");
        Circle[] separated = { a, new(50f, 0f, 20f) };
        Check(!HorizontalCoverage.IsCovered(separated, 0, 20f, 0f), "gap keeps both edges visible");

        Circle[] identical = { a, a, a };
        for (int i = 0; i < count; i++)
        {
            HorizontalCoverage.Sample(a, i, count, 0.17f, out float x, out float z);
            Check(!HorizontalCoverage.IsCovered(identical, 0, x, z), "first coincident ring remains visible");
            Check(HorizontalCoverage.IsCovered(identical, 1, x, z), "second coincident ring is hidden");
            Check(HorizontalCoverage.IsCovered(identical, 2, x, z), "third coincident ring is hidden");
        }

        Circle shifted = new(100f, -80f, 20f);
        HorizontalCoverage.Sample(shifted, 0, 80, 0f, out float sx, out float sz);
        Check(sx == 100f && sz == -60f, "world center offsets boundary");
        Check(HorizontalCoverage.SegmentCount(new Circle(0f, 0f, 0.1f)) == 3, "small circle remains drawable");
        Reject(() => new Circle(0f, 0f, 0f));
        Reject(() => new Circle(0f, 0f, float.NaN));
        Reject(() => new Circle(float.PositiveInfinity, 0f, 20f));
        Reject(() => HorizontalCoverage.IsCovered(alone, 1, 0f, 0f));
        Reject(() => HorizontalCoverage.Sample(a, 80, 80, 0f, out _, out _));
        Console.WriteLine("Spawn protection horizontal coverage checks passed");
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
