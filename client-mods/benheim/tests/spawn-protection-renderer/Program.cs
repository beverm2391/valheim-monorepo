using System;
using System.Collections.Generic;
using System.Linq;
using BenheimQoL.Infrastructure;
using BenheimQoL.Shortcuts;
using BenheimQoL.SpawnProtection;
using UnityEngine;
using UnityEngine.UI;
using Circle = BenheimQoL.SpawnProtection.HorizontalCoverage.Circle;
using UObject = UnityEngine.Object;

internal static partial class Program
{
    private const int OperationsPerFrame = 32;
    // Positions are stored as Vector3 floats, while the coverage mask reasons
    // about circle intersections in double precision. Points this close to an
    // intersection can round to either side without changing visible coverage.
    private const double PointBoundaryToleranceMeters = 1e-5d;

    private readonly struct CoverageEvidence
    {
        internal CoverageEvidence(int maskVisibleSamples, int definitelyVisibleSamples, int ambiguousBoundarySamples)
        {
            MaskVisibleSamples = maskVisibleSamples;
            DefinitelyVisibleSamples = definitelyVisibleSamples;
            AmbiguousBoundarySamples = ambiguousBoundarySamples;
        }
        internal int MaskVisibleSamples { get; }
        internal int DefinitelyVisibleSamples { get; }
        internal int AmbiguousBoundarySamples { get; }
    }

    private static void Main()
    {
        TestConfigAndF8ShareOneState();
        TestDenseRendererFixture(215, columns: 15, spacing: 0.8f, radius: 3f);
        TestDenseRendererFixture(1000, columns: 30, spacing: 0.8f, radius: 3f);
        TestDenseRendererFixture(215, columns: 15, spacing: 0.8f, radius: 20f);
        TestHighExposureNativeRadiusFixture();
        TestGeometryCacheAndTerrainResampling();
        TestFootprintRebuildPublishesOnlyWhenReady();
        TestLoadedAreaLifecycleAndCleanup();
        Console.WriteLine("Spawn protection renderer checks passed (Unity work is stubbed; no real-game timing claim).");
    }
}
