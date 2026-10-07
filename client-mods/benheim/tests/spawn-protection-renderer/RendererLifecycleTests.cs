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
    private static void TestConfigAndF8ShareOneState()
    {
        Setup();
        Toggle config = ShortcutOverlay.TestBuildSpawnProtectionConfig();
        Require(!config.isOn && !SpawnProtectionOverlay.Enabled, "config starts from the shared disabled renderer state");

        PressF8(viaUnityInput: true);
        Require(SpawnProtectionOverlay.Enabled, "Unity F8 enables the overlay");
        ShortcutOverlay.TestRefreshSpawnProtectionConfig();
        Require(config.isOn, "config refresh reflects F8 without invoking its callback");

        config.onValueChanged.Invoke(false);
        Require(!SpawnProtectionOverlay.Enabled && !config.isOn, "config disables the same overlay state");

        PressF8(viaUnityInput: false);
        Require(SpawnProtectionOverlay.Enabled, "ZInput F8 re-enables the same overlay state");
        ShortcutOverlay.TestRefreshSpawnProtectionConfig();
        Require(config.isOn, "config refresh reflects ZInput F8");
        SpawnProtectionOverlay.Reset("test_complete");
    }

    private static void TestGeometryCacheAndTerrainResampling()
    {
        Setup();
        EffectArea.Areas.Add(new EffectArea(1, 0f, 0f, 2f));
        SpawnProtectionOverlay.SetEnabled(true, "test_cache");
        DiagnosticEvent first = FinishCurrentBuild();
        int exposed = EvaluateCoverage(EffectArea.Areas).MaskVisibleSamples;
        CheckBuild(first, areaCount: 1, totalSamples: 8, exposed, "initial terrain projection");

        GameObject root = CurrentRoot();
        int initialRaycasts = Physics.Calls;
        int initialInstantiations = UObject.GameObjectInstantiations;
        int initialBuildEvents = BuildEvents().Count;
        while (Time.unscaledTime < 1.75f)
        {
            Time.unscaledTime += 1f / 60f;
            TickWithWorkLimit();
        }
        Require(Physics.Calls == initialRaycasts, "unchanged footprints do not raycast again before the terrain refresh interval");
        Require(UObject.GameObjectInstantiations == initialInstantiations, "unchanged footprints reuse all marker objects");
        Require(BuildEvents().Count == initialBuildEvents, "unchanged static geometry does not rebuild each frame");

        float oldY = root.Children.First(child => child.name == "Circle_section(Clone)").transform.position.y;
        Physics.Height = (x, z) => 7f + x * 0.25f - z * 0.125f;
        Heightmap.CurrentBiome = Heightmap.Biome.AshLands;
        int raysBeforeResample = Physics.Calls;
        int objectsBeforeResample = UObject.GameObjectInstantiations;
        int buildsBeforeResample = BuildEvents().Count;
        Time.unscaledTime += 0.5f;
        TickWithWorkLimit();
        Require(BuildEvents().Count == buildsBeforeResample + 1, "terrain refresh completes as a separate projection pass");
        DiagnosticEvent resampled = BuildEvents().Last();
        CheckBuild(resampled, areaCount: 1, totalSamples: 8, exposed, "terrain refresh", geometryChanged: false);
        Require(Physics.Calls - raysBeforeResample == exposed, "terrain refresh raycasts only currently exposed samples");
        Require(UObject.GameObjectInstantiations == objectsBeforeResample, "terrain refresh reuses cached marker geometry");
        Require(Physics.Calls - initialRaycasts == exposed, "terrain refresh performs one query for each exposed marker");
        Require(root.Children.First(child => child.name == "Circle_section(Clone)").transform.position.y != oldY,
            "terrain refresh updates marker height");
        foreach (GameObject marker in root.Children)
        {
            if (marker.name != "Circle_section(Clone)" || !marker.activeSelf) continue;
            Vector3 position = marker.transform.position;
            float expectedY = Physics.Height(position.x, position.z) + 0.5f;
            Require(Math.Abs(position.y - expectedY) < 0.0001f, "terrain resampling follows the hit height and Ashlands offset");
        }
        SpawnProtectionOverlay.Reset("test_complete");
    }

    private static void TestLoadedAreaLifecycleAndCleanup()
    {
        Setup();
        var firstArea = new EffectArea(10, 0f, 0f, 2f);
        EffectArea.Areas.Add(firstArea);
        SpawnProtectionOverlay.SetEnabled(true, "test_lifecycle");
        DiagnosticEvent firstBuild = FinishCurrentBuild();
        int oneAreaVisible = EvaluateCoverage(EffectArea.Areas).MaskVisibleSamples;
        CheckBuild(firstBuild, 1, 8, oneAreaVisible, "initial area");

        var secondArea = new EffectArea(20, 3.5f, 0f, 2f);
        EffectArea.Areas.Add(secondArea);
        DiagnosticEvent added = RefreshAfterChange(expectBuild: true, expectedAreas: 2);
        CheckBuild(added, 2, 16, EvaluateCoverage(EffectArea.Areas).MaskVisibleSamples, "added area");

        secondArea.transform.position = new Vector3(6f, 0f, 0f);
        DiagnosticEvent moved = RefreshAfterChange(expectBuild: true, expectedAreas: 2);
        CheckBuild(moved, 2, 16, EvaluateCoverage(EffectArea.Areas).MaskVisibleSamples, "moved area");

        EffectArea.Areas.Remove(secondArea);
        DiagnosticEvent removed = RefreshAfterChange(expectBuild: true, expectedAreas: 1);
        CheckBuild(removed, 1, 8, EvaluateCoverage(EffectArea.Areas).MaskVisibleSamples, "removed area");

        var loadingArea = new EffectArea(30, 0f, 4f, 2f) { isActiveAndEnabled = false };
        EffectArea.Areas.Add(loadingArea);
        RefreshAfterChange(expectBuild: false, expectedAreas: 1);
        loadingArea.isActiveAndEnabled = true; // The area becomes visible when its chunk loads.
        DiagnosticEvent loaded = RefreshAfterChange(expectBuild: true, expectedAreas: 2);
        CheckBuild(loaded, 2, 16, EvaluateCoverage(EffectArea.Areas).MaskVisibleSamples, "loaded area");

        GameObject beforeOff = CurrentRoot();
        Material ownedMaterial = beforeOff.Children
            .Where(child => child.name == "Circle_section(Clone)")
            .Select(child => child.GetComponent<Renderer>()!.sharedMaterial!)
            .First();
        PressF8(viaUnityInput: true);
        Require(!SpawnProtectionOverlay.Enabled, "F8 turns the overlay off");
        Require(beforeOff.destroyRequested && !beforeOff.activeSelf, "turning off hides the owned root and requests its destruction");
        Require(ownedMaterial.destroyRequested, "turning off requests destruction of the cloned marker material");
        Require(beforeOff.Children.All(child => !child.activeInHierarchy), "turning off hides every pooled marker");

        int configEnableBuilds = BuildEvents().Count;
        SpawnProtectionOverlay.SetEnabled(true, "config");
        GameObject afterConfigEnable = CurrentRoot();
        DiagnosticEvent reenabled = FinishCurrentBuild(configEnableBuilds);
        CheckBuild(reenabled, 2, 16, EvaluateCoverage(EffectArea.Areas).MaskVisibleSamples, "config re-enable");
        Require(!ReferenceEquals(beforeOff, afterConfigEnable), "re-enable creates a fresh owned root");

        Material secondOwnedMaterial = afterConfigEnable.Children
            .Where(child => child.name == "Circle_section(Clone)")
            .Select(child => child.GetComponent<Renderer>()!.sharedMaterial!)
            .First();
        SpawnProtectionOverlay.Reset("test_reset");
        Require(!SpawnProtectionOverlay.Enabled && afterConfigEnable.destroyRequested && secondOwnedMaterial.destroyRequested,
            "explicit reset removes the re-enabled renderer and its owned material");

        int resetEnableBuilds = BuildEvents().Count;
        SpawnProtectionOverlay.SetEnabled(true, "config");
        GameObject afterReset = CurrentRoot();
        DiagnosticEvent resetReenabled = FinishCurrentBuild(resetEnableBuilds);
        CheckBuild(resetReenabled, 2, 16, EvaluateCoverage(EffectArea.Areas).MaskVisibleSamples, "reset re-enable");
        Require(!ReferenceEquals(afterConfigEnable, afterReset), "reenabling after reset constructs fresh markers");
        SpawnProtectionOverlay.Reset("test_complete");
    }

    private static void TestFootprintRebuildPublishesOnlyWhenReady()
    {
        Setup();
        EffectArea.Areas.Add(new EffectArea(9999, 0f, 0f, 2f));
        SpawnProtectionOverlay.SetEnabled(true, "test_rebuild_visibility");
        FinishCurrentBuild();

        GameObject root = CurrentRoot();
        Require(root.activeSelf && ActiveMarkerCount(root) == 8, "initial footprint is visible before a change");
        HashSet<(float X, float Z)> previousBoundary = PointOracleVisiblePositions(EffectArea.Areas);
        int previousBuildCount = BuildEvents().Count;

        EffectArea.Areas.Clear();
        EffectArea.Areas.AddRange(DenseAreas(215, columns: 15, spacing: 0.8f, radius: 3f));
        Time.unscaledTime += 0.51f;
        TickWithWorkLimit();
        Require(BuildEvents().Count == previousBuildCount, "dense footprint change cannot complete in its detection update");
        Require(!root.activeSelf, "old boundary is hidden as soon as the new footprint snapshot is accepted");

        DiagnosticEvent? rebuilt = null;
        for (int frame = 0; frame < 5000 && BuildEvents().Count == previousBuildCount; frame++)
        {
            Time.unscaledTime += 1f / 60f;
            TickWithWorkLimit();
            if (BuildEvents().Count == previousBuildCount)
                Require(!root.activeSelf, "old or partially updated boundary stays hidden during every pending frame");
            else
                rebuilt = BuildEvents().Last();
        }
        if (rebuilt == null) throw new Exception("dense replacement boundary did not complete");

        CoverageEvidence coverage = EvaluateCoverage(EffectArea.Areas);
        CheckBuild(rebuilt, 215, 2580, coverage.MaskVisibleSamples, "completed footprint replacement");
        Require(root.activeSelf, "replacement boundary is published after projection and pool trimming finish");
        Require(ActiveMarkerCount(root) == coverage.MaskVisibleSamples,
            "published boundary has exactly the new exterior marker count");

        HashSet<(float X, float Z)> expectedBoundary = PointOracleVisiblePositions(EffectArea.Areas);
        HashSet<(float X, float Z)> actualBoundary = root.Children
            .Where(marker => marker.name == "Circle_section(Clone)" && marker.activeInHierarchy)
            .Select(marker => (marker.transform.position.x, marker.transform.position.z))
            .ToHashSet();
        Require(actualBoundary.SetEquals(expectedBoundary), "published marker positions match only the new exposed boundary");
        Require(previousBoundary.All(point => !actualBoundary.Contains(point)), "no marker from the removed footprint remains visible");
        SpawnProtectionOverlay.Reset("test_complete");
    }

    private static DiagnosticEvent RefreshAfterChange(bool expectBuild, int expectedAreas)
    {
        int priorBuildCount = BuildEvents().Count;
        int priorEnumerations = EffectArea.EnumerationCount;
        int priorRaycasts = Physics.Calls;
        Time.unscaledTime += 0.51f;
        TickWithWorkLimit();
        Require(EffectArea.EnumerationCount == priorEnumerations + 1, "area changes are discovered by the periodic loaded-area refresh");
        if (!expectBuild)
        {
            Require(BuildEvents().Count == priorBuildCount, "inactive unloaded area does not rebuild visible geometry");
            return BuildEvents().Last();
        }
        DiagnosticEvent built = FinishCurrentBuild(priorBuildCount);
        Require(Field<int>(built, "areas") == expectedAreas, "rebuild reflects the current loaded area set");
        Require(Physics.Calls - priorRaycasts == Field<int>(built, "raycasts"),
            "area rebuild raycasts exactly the exposed sample set");
        return built;
    }
}
