using System;
using System.Collections.Generic;
using System.Linq;
using BenheimQoL.Infrastructure;
using BenheimQoL.SpawnProtection;
using UnityEngine;
using Circle = BenheimQoL.SpawnProtection.HorizontalCoverage.Circle;
using UObject = UnityEngine.Object;
internal static partial class Program
{
    private static void TestDenseRendererFixture(int areaCount, int columns, float spacing, float radius)
    {
        Setup();
        EffectArea.Areas.AddRange(DenseAreas(areaCount, columns, spacing, radius));
        int totalSamples = areaCount * HorizontalCoverage.SegmentCount(new Circle(0f, 0f, radius));

        SpawnProtectionOverlay.SetEnabled(true, "test_dense_fixture");
        DiagnosticEvent built = FinishCurrentBuild();
        CoverageEvidence coverage = EvaluateCoverage(EffectArea.Areas);
        CheckBuild(built, areaCount, totalSamples, coverage.MaskVisibleSamples, "dense fixture");
        Require(coverage.MaskVisibleSamples >= coverage.DefinitelyVisibleSamples
            && coverage.MaskVisibleSamples <= coverage.DefinitelyVisibleSamples + coverage.AmbiguousBoundarySamples,
            $"dense {areaCount}-area fixture matches the point oracle away from float-rounded tangencies");
        Require(coverage.MaskVisibleSamples < totalSamples / 3,
            $"dense {areaCount}-area fixture hides a substantial share of its boundary samples");
        Require(Field<int>(built, "work_frames") >= (areaCount + OperationsPerFrame - 1) / OperationsPerFrame,
            $"dense {areaCount}-area fixture spreads geometry across the scheduled work frames");
        Require(UObject.GameObjectInstantiations == coverage.MaskVisibleSamples,
            $"dense {areaCount}-area fixture instantiates only exposed markers");
        Require(Physics.Calls == coverage.MaskVisibleSamples,
            $"dense {areaCount}-area fixture raycasts only exposed samples");
        Require(ActiveMarkerCount(CurrentRoot()) == coverage.MaskVisibleSamples,
            $"dense {areaCount}-area fixture activates only exposed markers");
        int workFrames = Field<int>(built, "work_frames");
        Console.WriteLine($"  dense fixture: areas={areaCount}, radius={radius}m, all samples={totalSamples}, " +
            $"exposed={coverage.MaskVisibleSamples}, endpoint-ambiguous={coverage.AmbiguousBoundarySamples}, " +
            $"work-frames={workFrames}");
    }

    private static void TestHighExposureNativeRadiusFixture()
    {
        Setup();
        int id = 1;
        foreach (float x in new[] { -60f, 0f, 60f })
        foreach (float z in new[] { -60f, 0f, 60f })
            EffectArea.Areas.Add(new EffectArea(id++, x, z, 20f));

        SpawnProtectionOverlay.SetEnabled(true, "test_high_exposure_fixture");
        DiagnosticEvent built = FinishCurrentBuild();
        CoverageEvidence coverage = EvaluateCoverage(EffectArea.Areas);
        const int areas = 9;
        const int samples = areas * 80;
        CheckBuild(built, areas, samples, samples, "high-exposure native-radius fixture");
        Require(coverage.DefinitelyVisibleSamples == samples && coverage.AmbiguousBoundarySamples == 0,
            "separated native-radius areas expose every sample without point-oracle boundary ambiguity");
        Require(Field<int>(built, "work_frames") >= (areas + samples + OperationsPerFrame - 1) / OperationsPerFrame,
            "high-exposure projection spreads all marker work across capped updates");
        Require(UObject.GameObjectInstantiations == samples, "high-exposure fixture retains all 720 markers");
        Require(Physics.Calls == samples, "high-exposure fixture raycasts each visible marker once");
        Require(ActiveMarkerCount(CurrentRoot()) == samples, "high-exposure fixture publishes all 720 markers");
        Console.WriteLine($"  high-exposure fixture: areas={areas}, all samples={samples}, " +
            $"retained={coverage.MaskVisibleSamples}, work-frames={Field<int>(built, "work_frames")}");
        SpawnProtectionOverlay.Reset("test_complete");
    }

    private static CoverageEvidence EvaluateCoverage(IReadOnlyList<EffectArea> input)
    {
        var areas = input
            .Where(area => area.isActiveAndEnabled && (area.m_type & EffectArea.Type.PlayerBase) != 0)
            .OrderBy(area => area.GetInstanceID())
            .Select(area =>
            {
                Vector3 position = area.transform.position;
                float radius = area.GetRadius() * Math.Abs(area.transform.lossyScale.x);
                return (Id: area.GetInstanceID(), X: position.x, Z: position.z, Radius: radius);
            })
            .Where(area => area.X * area.X + area.Z * area.Z <= (SpawnProtectionOverlay.ViewDistance + area.Radius) * (SpawnProtectionOverlay.ViewDistance + area.Radius))
            .ToArray();
        Circle[] circles = areas.Select(area => new Circle(area.X, area.Z, area.Radius)).ToArray();
        var scratch = new int[circles.Length == 0 ? 1 : circles.Max(circle => HorizontalCoverage.SegmentCount(circle)) + 1];
        int maskVisible = 0;
        int definitelyVisible = 0;
        int ambiguous = 0;
        for (int owner = 0; owner < circles.Length; owner++)
        {
            Circle source = circles[owner];
            int count = HorizontalCoverage.SegmentCount(source);
            HorizontalCoverage.BuildCoverage(circles, owner, scratch);
            for (int sample = 0; sample < count; sample++)
            {
                double angle = sample * (Math.PI * 2d / count);
                float x = source.X + (float)Math.Sin(angle) * source.Radius;
                float z = source.Z + (float)Math.Cos(angle) * source.Radius;
                bool definitelyCovered = false;
                bool boundaryAmbiguous = false;
                for (int otherIndex = 0; otherIndex < circles.Length; otherIndex++)
                {
                    if (otherIndex == owner) continue;
                    Circle other = circles[otherIndex];
                    bool identical = source.X == other.X && source.Z == other.Z && source.Radius == other.Radius;
                    if (identical)
                    {
                        if (otherIndex < owner) { definitelyCovered = true; break; }
                        continue;
                    }
                    double dx = (double)x - other.X;
                    double dz = (double)z - other.Z;
                    double margin = Math.Sqrt(dx * dx + dz * dz) - other.Radius;
                    if (margin < -PointBoundaryToleranceMeters) { definitelyCovered = true; break; }
                    if (Math.Abs(margin) <= PointBoundaryToleranceMeters) boundaryAmbiguous = true;
                }

                bool maskCovered = scratch[sample] != 0;
                if (definitelyCovered)
                    Require(maskCovered, $"coverage mask hides every sample clearly inside another circle (area {areas[owner].Id}, sample {sample})");
                else if (!boundaryAmbiguous)
                {
                    Require(!maskCovered, $"coverage mask preserves every sample clearly outside other circles (area {areas[owner].Id}, sample {sample})");
                    definitelyVisible++;
                }
                else ambiguous++;

                if (!maskCovered) maskVisible++;
            }
        }
        return new CoverageEvidence(maskVisible, definitelyVisible, ambiguous);
    }

    private static HashSet<(float X, float Z)> PointOracleVisiblePositions(IReadOnlyList<EffectArea> input)
    {
        var areas = input
            .Where(area => area.isActiveAndEnabled && (area.m_type & EffectArea.Type.PlayerBase) != 0)
            .OrderBy(area => area.GetInstanceID())
            .Select(area =>
            {
                Vector3 position = area.transform.position;
                float radius = area.GetRadius() * Math.Abs(area.transform.lossyScale.x);
                return (X: position.x, Z: position.z, Radius: radius);
            })
            .Where(area => area.X * area.X + area.Z * area.Z <= (SpawnProtectionOverlay.ViewDistance + area.Radius) * (SpawnProtectionOverlay.ViewDistance + area.Radius))
            .ToArray();
        var visible = new HashSet<(float X, float Z)>();
        for (int owner = 0; owner < areas.Length; owner++)
        {
            var source = areas[owner];
            int count = HorizontalCoverage.SegmentCount(new Circle(source.X, source.Z, source.Radius));
            for (int sample = 0; sample < count; sample++)
            {
                double angle = sample * (Math.PI * 2d / count);
                float x = source.X + (float)Math.Sin(angle) * source.Radius;
                float z = source.Z + (float)Math.Cos(angle) * source.Radius;
                bool covered = false;
                for (int otherIndex = 0; otherIndex < areas.Length; otherIndex++)
                {
                    if (otherIndex == owner) continue;
                    var other = areas[otherIndex];
                    bool identical = source.X == other.X && source.Z == other.Z && source.Radius == other.Radius;
                    if (identical)
                    {
                        if (otherIndex < owner) { covered = true; break; }
                        continue;
                    }
                    double dx = (double)x - other.X;
                    double dz = (double)z - other.Z;
                    if (dx * dx + dz * dz <= (double)other.Radius * other.Radius) { covered = true; break; }
                }
                if (!covered) visible.Add((x, z));
            }
        }
        return visible;
    }

    private static List<EffectArea> DenseAreas(int count, int columns, float spacing, float radius)
    {
        var areas = new List<EffectArea>(count);
        int rows = (count + columns - 1) / columns;
        for (int index = 0; index < count; index++)
        {
            int column = index % columns;
            int row = index / columns;
            float x = (column - (columns - 1) * 0.5f) * spacing;
            float z = (row - (rows - 1) * 0.5f) * spacing;
            areas.Add(new EffectArea(index + 1, x, z, radius));
        }
        return areas;
    }
}
