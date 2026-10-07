using System;
using System.Collections.Generic;
using System.Diagnostics;
using BenheimQoL.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BenheimQoL.SpawnProtection;

// Adapted from ESP 1.40 Projectors.cs (Unlicense), at the commit referenced in
// HorizontalCoverage. Only our visual clones are owned here: no native station,
// CircleProjector, EffectArea, collider, or network object is modified.
internal static class SpawnProtectionOverlay
{
    internal const float ViewDistance = 80f;
    // Only exposed samples have markers. Geometry is cached until the loaded
    // footprints change; terrain is resampled periodically for terraforming.
    // Work is spread over frames so turning this on cannot instantiate a dense
    // base's entire collection of hidden circles on the gameplay thread.
    private const int MaxOperationsPerFrame = 32;
    private const double FrameBudgetMs = 2d;
    private readonly struct Area
    {
        internal Area(int id, HorizontalCoverage.Circle circle, float height) { Id = id; Circle = circle; Height = height; }
        internal int Id { get; }
        internal HorizontalCoverage.Circle Circle { get; }
        internal float Height { get; }
        internal bool Same(Area other) => Id == other.Id && Height == other.Height
            && Circle.X == other.Circle.X && Circle.Z == other.Circle.Z && Circle.Radius == other.Circle.Radius;
    }
    private readonly struct Sample
    {
        internal Sample(Vector3 position, Vector3 tangent) { Position = position; Tangent = tangent; }
        internal Vector3 Position { get; }
        internal Vector3 Tangent { get; }
    }
    private static readonly List<Area> Areas = new();
    private static readonly List<Area> Candidate = new();
    private static readonly List<HorizontalCoverage.Circle> Circles = new();
    private static readonly List<Sample> Samples = new();
    private static readonly List<GameObject> Markers = new();
    private static readonly Stopwatch WorkClock = new();
    private static GameObject? root;
    private static GameObject? segmentPrefab;
    private static Material? markerMaterial;
    private static int terrainMask;
    private static int[] coverage = Array.Empty<int>();
    private static float refreshAt;
    private static float hudAt;
    private static float terrainAt;
    private static int owner;
    private static int projected;
    private static int trimmed;
    private static int sampled;
    private static int raycasts;
    private static int workFrames;
    private static float workStartedAt;
    private static double maxFrameMs;
    private static double maxUpdateMs;
    private static double enableMs;
    private static bool geometryChanged;
    private static bool boundaryReady;
    private static bool building;
    private static bool projecting;

    internal static bool Enabled { get; private set; }

    internal static void Update()
    {
        long frameStarted = Stopwatch.GetTimestamp();
        if (Input.GetKeyDown(KeyCode.F8) || ZInput.GetKeyDown(KeyCode.F8))
        {
            string? rejection = ShortcutRejection();
            if (rejection != null) Emit("rejected", "f8", rejection);
            else SetEnabled(!Enabled, "f8");
        }
        if (!Enabled) return;
        if (!HealthReporting.GameplayActionsEnabled || Player.m_localPlayer == null || ZNetScene.instance == null)
        {
            Reset("gameplay_unavailable");
            return;
        }
        try
        {
            if (!building && !projecting && Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + 0.5f;
                Refresh(Player.m_localPlayer.transform.position);
            }
            ProcessWork();
            // HUD membership is independent of boundary construction progress.
            if (Time.unscaledTime >= hudAt)
            {
                hudAt = Time.unscaledTime + 0.1f;
                SpawnProtectionMinimapIndicator.Update();
            }
            // Include the registry scan, sorting and native HUD query as well
            // as scheduled work. The 2 ms target applies to construction, not
            // a hard deadline for these native calls or an individual clone.
            maxUpdateMs = Math.Max(maxUpdateMs, ElapsedMs(frameStarted));
            if (boundaryReady) EmitBoundary();
        }
        catch (Exception exception) { Fail(exception, "update"); }
    }

    internal static void SetEnabled(bool enabled, string source)
    {
        if (!enabled) { Reset(source); return; }
        if (Enabled) return;
        if (!HealthReporting.GameplayActionsEnabled || Player.m_localPlayer == null || ZNetScene.instance == null)
        {
            Emit("rejected", source, "gameplay_unavailable");
            return;
        }
        try
        {
            long enableStarted = Stopwatch.GetTimestamp();
            // Loaded 1.0.16 inspection: piece_workbench/AreaMarker, mask 2048;
            // Circle_section has only Transform/MeshFilter/MeshRenderer and an
            // Unlit/Color material with _Color. Retain its shader and scale;
            // recolor one owned material shared exclusively by our clones.
            GameObject workbench = ZNetScene.instance.GetPrefab("piece_workbench");
            CircleProjector? projector = workbench != null
                ? workbench.GetComponentInChildren<CircleProjector>(includeInactive: true) : null;
            segmentPrefab = projector?.m_prefab;
            if (segmentPrefab == null || segmentPrefab.GetComponent<Renderer>() == null || projector!.m_mask.value == 0)
                throw new InvalidOperationException("Native workbench range marker is unavailable.");
            Material nativeMaterial = segmentPrefab.GetComponent<Renderer>().sharedMaterial;
            if (nativeMaterial == null || !nativeMaterial.HasProperty("_Color"))
                throw new InvalidOperationException("Native workbench marker color is unavailable.");
            markerMaterial = new Material(nativeMaterial)
            {
                name = "BenheimSpawnProtectionGreen",
                color = Color.green
            };
            terrainMask = projector.m_mask.value;
            root = new GameObject("BenheimSpawnProtectionOverlay");
            Enabled = true;
            Refresh(Player.m_localPlayer.transform.position);
            SpawnProtectionMinimapIndicator.Update();
            refreshAt = Time.unscaledTime + 0.5f;
            hudAt = Time.unscaledTime + 0.1f;
            enableMs = ElapsedMs(enableStarted);
            Emit("enabled", source, "horizontal_preview");
        }
        catch (Exception exception) { Fail(exception, source); }
    }

    internal static void Reset(string reason)
    {
        bool wasEnabled = Enabled;
        Enabled = false;
        SpawnProtectionMinimapIndicator.Reset();
        // Hide synchronously before deferred Destroy; toggling again in this
        // frame cannot leave an old boundary visible.
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
        if (markerMaterial != null) Object.Destroy(markerMaterial);
        markerMaterial = null;
        root = null;
        segmentPrefab = null;
        Areas.Clear();
        Candidate.Clear();
        Circles.Clear();
        Samples.Clear();
        Markers.Clear();
        coverage = Array.Empty<int>();
        building = projecting = false;
        owner = projected = trimmed = sampled = raycasts = 0;
        workFrames = 0;
        workStartedAt = 0f;
        maxFrameMs = 0d;
        maxUpdateMs = enableMs = 0d;
        boundaryReady = false;
        refreshAt = hudAt = terrainAt = 0f;
        if (wasEnabled) Emit("disabled", reason, "visuals_removed");
    }

    private static string? ShortcutRejection()
    {
        if (!HealthReporting.GameplayActionsEnabled) return "gameplay_disabled";
        if (InputState.IsTextEntryActive() || Chat.instance?.HasFocus() == true) return "text_or_benheim_menu";
        if (Menu.IsVisible() || InventoryGui.IsVisible() || Minimap.IsOpen() || StoreGui.IsVisible()
            || UnifiedPopup.IsVisible() || Hud.IsPieceSelectionVisible() || Hud.InRadial()) return "native_menu";
        if (Player.m_localPlayer == null) return "player_unavailable";
        if (Player.m_localPlayer.IsDead() || Player.m_localPlayer.InCutscene() || Player.m_localPlayer.IsTeleporting()) return "player_busy";
        return null;
    }

    private static void Refresh(Vector3 playerPosition)
    {
        Candidate.Clear();
        foreach (EffectArea area in EffectArea.GetAllAreas())
        {
            if (area == null || !area.isActiveAndEnabled || (area.m_type & EffectArea.Type.PlayerBase) == 0) continue;
            Vector3 position = area.transform.position;
            float radius = area.GetRadius() * Mathf.Abs(area.transform.lossyScale.x);
            float reach = ViewDistance + radius;
            float dx = position.x - playerPosition.x;
            float dz = position.z - playerPosition.z;
            if (dx * dx + dz * dz > reach * reach) continue;
            Candidate.Add(new Area(area.GetInstanceID(), new HorizontalCoverage.Circle(position.x, position.z, radius), position.y));
        }
        // EffectArea registration order can change as chunks load. Stable ids
        // preserve coincident-circle ownership and avoid pointless rebuilds.
        Candidate.Sort((a, b) => a.Id.CompareTo(b.Id));
        bool same = Candidate.Count == Areas.Count;
        for (int i = 0; same && i < Candidate.Count; i++) same = Candidate[i].Same(Areas[i]);
        if (!same)
        {
            // Publish one complete footprint snapshot. Keeping old markers
            // visible while reusing the pool would briefly show removed pieces
            // or a mixture of old and new coverage during a scheduled rebuild.
            root!.SetActive(false);
            Areas.Clear();
            Areas.AddRange(Candidate);
            Circles.Clear();
            foreach (Area area in Areas) Circles.Add(area.Circle);
            Samples.Clear();
            owner = sampled = 0;
            building = true;
            BeginWork(true);
        }
        else if (Time.unscaledTime >= terrainAt) { BeginWork(false); BeginProjection(); }
    }

    private static void BeginWork(bool changed)
    {
        geometryChanged = changed;
        workFrames = 0;
        workStartedAt = Time.unscaledTime;
        maxFrameMs = 0d;
    }

    private static void BeginProjection()
    {
        projected = trimmed = raycasts = 0;
        projecting = true;
        terrainAt = Time.unscaledTime + 2f;
    }

    private static void ProcessWork()
    {
        if (!building && !projecting) return;
        workFrames++;
        WorkClock.Restart();
        int operations = 0;
        while (operations < MaxOperationsPerFrame && WorkClock.Elapsed.TotalMilliseconds < FrameBudgetMs)
        {
            if (building)
            {
                if (owner == Circles.Count) { building = false; BeginProjection(); continue; }
                HorizontalCoverage.Circle circle = Circles[owner];
                int count = HorizontalCoverage.SegmentCount(circle);
                if (coverage.Length < count + 1) coverage = new int[count + 1];
                HorizontalCoverage.BuildCoverage(Circles, owner, coverage);
                sampled += count;
                for (int i = 0; i < count; i++)
                {
                    if (coverage[i] != 0) continue;
                    HorizontalCoverage.Sample(circle, i, count, 0f, out float x, out float z);
                    // Analytical tangent avoids snapping hidden neighbors just
                    // to orient a visible marker. Project onto the terrain normal
                    // below to retain the native slope-following appearance.
                    Samples.Add(new Sample(new Vector3(x, Areas[owner].Height, z),
                        new Vector3((z - circle.Z) / circle.Radius, 0f, -(x - circle.X) / circle.Radius)));
                }
                owner++;
            }
            else if (projected < Samples.Count)
            {
                Sample sample = Samples[projected];
                Vector3 position = sample.Position;
                Vector3 tangent = sample.Tangent;
                raycasts++;
                if (Physics.Raycast(position + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f, terrainMask))
                {
                    position.y = hit.point.y;
                    if (Heightmap.FindBiome(position) == Heightmap.Biome.AshLands) position.y += 0.5f;
                    tangent = Vector3.ProjectOnPlane(tangent, hit.normal);
                }
                if (projected == Markers.Count)
                {
                    GameObject marker = Object.Instantiate(segmentPrefab!, root!.transform);
                    Markers.Add(marker); // Register ownership before configuring.
                    marker.GetComponent<Renderer>().sharedMaterial = markerMaterial!;
                }
                GameObject segment = Markers[projected++];
                segment.transform.position = position;
                segment.transform.rotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                segment.SetActive(true);
            }
            else
            {
                if (trimmed < Samples.Count) trimmed = Samples.Count;
                if (trimmed < Markers.Count) Markers[trimmed++].SetActive(false);
                else
                {
                    if (geometryChanged) root!.SetActive(true);
                    projecting = false;
                    break;
                }
            }
            operations++;
        }
        WorkClock.Stop();
        maxFrameMs = Math.Max(maxFrameMs, WorkClock.Elapsed.TotalMilliseconds);
        if (!building && !projecting) boundaryReady = true;
    }

    private static double ElapsedMs(long started) => (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

    private static void EmitBoundary()
    {
        boundaryReady = false;
        Diagnostics.Emit(DiagnosticEvent.Create("SpawnProtection", "boundary_built")
                .Integer("areas", Areas.Count).Integer("samples", sampled).Integer("visible_segments", Samples.Count)
                .Integer("pooled_segments", Markers.Count).Integer("raycasts", raycasts)
                .Number("max_work_frame_ms", maxFrameMs).Integer("frame_operation_limit", MaxOperationsPerFrame)
                .Number("max_update_frame_ms", maxUpdateMs)
                .Boolean("geometry_changed", geometryChanged).Integer("work_frames", workFrames)
                .Number("elapsed_ms", (Time.unscaledTime - workStartedAt) * 1000d));
        maxFrameMs = maxUpdateMs = 0d;
    }

    private static void Fail(Exception exception, string source)
    {
        Reset("render_failure");
        Plugin.Log.LogWarning($"Spawn protection overlay failed: {exception}");
        Emit("failed", source, exception.Message);
        Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Spawn protection overlay unavailable. See the Benheim diagnostic log.");
    }

    private static void Emit(string result, string source, string reason)
    {
        Diagnostics.Emit(DiagnosticEvent.Create("SpawnProtection", "overlay_state")
            .String("result", result).String("source", source).String("reason", reason)
            .Boolean("enabled", Enabled).Integer("areas", Areas.Count).String("color", "bright_green")
            .Number("enable_ms", enableMs));
    }

}
