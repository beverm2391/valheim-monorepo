using System;
using System.Collections.Generic;
using System.Linq;
using BenheimQoL.Infrastructure;
using BenheimQoL.SpawnProtection;
using UnityEngine;
using UObject = UnityEngine.Object;
internal static partial class Program
{
    private static DiagnosticEvent FinishCurrentBuild(int priorBuildCount = 0)
    {
        for (int frame = 0; frame < 5000; frame++)
        {
            if (BuildEvents().Count > priorBuildCount) return BuildEvents().Last();
            Time.unscaledTime += 1f / 60f;
            TickWithWorkLimit();
        }
        throw new Exception("renderer did not finish its scheduled boundary pass");
    }

    private static void TickWithWorkLimit()
    {
        int beforeRays = Physics.Calls;
        int beforeObjects = UObject.GameObjectInstantiations;
        Input.F8Down = false;
        ZInput.F8Down = false;
        SpawnProtectionOverlay.Update();
        int rays = Physics.Calls - beforeRays;
        int objects = UObject.GameObjectInstantiations - beforeObjects;
        Require(rays <= OperationsPerFrame, $"one update raycasts no more than {OperationsPerFrame} exposed samples");
        Require(objects <= OperationsPerFrame, $"one update instantiates no more than {OperationsPerFrame} markers");
    }

    private static void PressF8(bool viaUnityInput)
    {
        int beforeRays = Physics.Calls;
        int beforeObjects = UObject.GameObjectInstantiations;
        Input.F8Down = viaUnityInput;
        ZInput.F8Down = !viaUnityInput;
        SpawnProtectionOverlay.Update();
        Input.F8Down = false;
        ZInput.F8Down = false;
        Require(Physics.Calls - beforeRays <= OperationsPerFrame, "F8 update respects the per-frame raycast cap");
        Require(UObject.GameObjectInstantiations - beforeObjects <= OperationsPerFrame, "F8 update respects the per-frame marker cap");
    }

    private static void CheckBuild(
        DiagnosticEvent built,
        int areaCount,
        int totalSamples,
        int exposedSamples,
        string label,
        bool geometryChanged = true)
    {
        Require(Field<int>(built, "areas") == areaCount, $"{label}: diagnostics count the loaded areas");
        Require(Field<int>(built, "samples") == totalSamples, $"{label}: all candidate sample positions are accounted for");
        int actualVisible = Field<int>(built, "visible_segments");
        Require(actualVisible == exposedSamples,
            $"{label}: renderer exposed {actualVisible} samples but coverage evidence expects {exposedSamples}");
        Require(Field<int>(built, "raycasts") == exposedSamples, $"{label}: terrain queries match exposed samples");
        Require(Field<int>(built, "frame_operation_limit") == OperationsPerFrame, $"{label}: renderer reports its per-update work cap");
        Require(Field<bool>(built, "geometry_changed") == geometryChanged, $"{label}: diagnostics distinguish geometry rebuilds from terrain-only passes");
        Require(Field<int>(built, "work_frames") >= 1, $"{label}: diagnostics count scheduled work frames");
    }

    private static List<DiagnosticEvent> BuildEvents() => Diagnostics.Events.Where(value => value.Name == "boundary_built").ToList();

    private static GameObject CurrentRoot() => GameObject.Instances.Last(value => value.name == "BenheimSpawnProtectionOverlay");

    private static int ActiveMarkerCount(GameObject root) => root.Children.Count(child =>
        child.name == "Circle_section(Clone)" && child.activeInHierarchy);

    private static T Field<T>(DiagnosticEvent value, string name) => (T)value.Fields[name]!;

    private static void Setup()
    {
        SpawnProtectionOverlay.Reset("test_setup");
        Diagnostics.Clear();
        EffectArea.Reset();
        SpawnProtectionMinimapIndicator.Clear();
        GameObject.ResetInstances();
        UObject.ResetCounters();
        Physics.Reset();
        Time.unscaledTime = 0f;
        Input.F8Down = false;
        ZInput.F8Down = false;
        InputState.TextEntryActive = false;
        Chat.instance = null;
        HealthReporting.GameplayActionsEnabled = true;
        Heightmap.CurrentBiome = Heightmap.Biome.Meadows;
        Player.m_localPlayer = new Player();

        var scene = new ZNetScene();
        var markerPrefab = new GameObject("Circle_section");
        markerPrefab.AddComponent<Renderer>().sharedMaterial = new Material { name = "NativeCircleMaterial" };
        var workbench = new GameObject("piece_workbench");
        CircleProjector projector = workbench.AddComponent<CircleProjector>();
        projector.m_prefab = markerPrefab;
        projector.m_mask.value = 2048;
        scene.AddPrefab("piece_workbench", workbench);
        ZNetScene.instance = scene;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
