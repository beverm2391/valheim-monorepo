using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<int, Ring> Rings = new();
    private static readonly List<int> Removed = new();
    private static readonly List<int> Selected = new();
    private static readonly List<HorizontalCoverage.Circle> Circles = new();
    private static GameObject? root;
    private static GameObject? segmentPrefab;
    private static int terrainMask;
    private static float refreshAt;
    private static float drawAt;

    internal static bool Enabled { get; private set; }

    internal static void Update()
    {
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
            if (Time.unscaledTime >= refreshAt)
            {
                refreshAt = Time.unscaledTime + 0.5f;
                Refresh(Player.m_localPlayer.transform.position);
            }
            if (Time.unscaledTime >= drawAt)
            {
                drawAt = Time.unscaledTime + 0.1f;
                Draw();
            }
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
            // Loaded 1.0.16 inspection: piece_workbench/AreaMarker, mask 2048;
            // Circle_section has only Transform/MeshFilter/MeshRenderer and an
            // Unlit/Color shared material. Retain its native material and scale.
            GameObject workbench = ZNetScene.instance.GetPrefab("piece_workbench");
            CircleProjector? projector = workbench != null
                ? workbench.GetComponentInChildren<CircleProjector>(includeInactive: true) : null;
            segmentPrefab = projector?.m_prefab;
            if (segmentPrefab == null || segmentPrefab.GetComponent<Renderer>() == null || projector!.m_mask.value == 0)
                throw new InvalidOperationException("Native workbench range marker is unavailable.");
            terrainMask = projector.m_mask.value;
            root = new GameObject("BenheimSpawnProtectionOverlay");
            Enabled = true;
            Refresh(Player.m_localPlayer.transform.position);
            Draw();
            refreshAt = Time.unscaledTime + 0.5f;
            drawAt = Time.unscaledTime + 0.1f;
            Emit("enabled", source, "horizontal_preview");
        }
        catch (Exception exception) { Fail(exception, source); }
    }

    internal static void Reset(string reason)
    {
        bool wasEnabled = Enabled;
        Enabled = false;
        // Hide synchronously before deferred Destroy; toggling again in this
        // frame cannot leave an old boundary visible.
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
        root = null;
        segmentPrefab = null;
        Rings.Clear();
        Selected.Clear();
        Circles.Clear();
        Removed.Clear();
        refreshAt = drawAt = 0f;
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
        Selected.Clear();
        Circles.Clear();
        foreach (EffectArea area in EffectArea.GetAllAreas())
        {
            if (area == null || !area.isActiveAndEnabled || (area.m_type & EffectArea.Type.PlayerBase) == 0) continue;
            Vector3 position = area.transform.position;
            float radius = area.GetRadius() * Mathf.Abs(area.transform.lossyScale.x);
            float reach = ViewDistance + radius;
            float dx = position.x - playerPosition.x;
            float dz = position.z - playerPosition.z;
            if (dx * dx + dz * dz > reach * reach) continue;
            HorizontalCoverage.Circle circle = new(position.x, position.z, radius);
            int id = area.GetInstanceID();
            Selected.Add(id);
            Circles.Add(circle);
            int count = HorizontalCoverage.SegmentCount(circle);
            if (!Rings.TryGetValue(id, out Ring? ring) || ring.Segments.Length != count)
            {
                ring?.Destroy();
                // Track ownership before cloning. On allocation failure Reset
                // can still destroy every object through the owned root.
                ring = new Ring(count, position.y);
                Rings[id] = ring;
                for (int i = 0; i < count; i++)
                    ring.Segments[i] = Object.Instantiate(segmentPrefab!, root!.transform);
            }
            ring.Height = position.y;
        }
        Removed.Clear();
        foreach (int id in Rings.Keys)
            if (!Selected.Contains(id)) Removed.Add(id);
        foreach (int id in Removed) { Rings[id].Destroy(); Rings.Remove(id); }
    }

    private static void Draw()
    {
        float phase = Time.time * 0.1f;
        for (int owner = 0; owner < Selected.Count; owner++)
        {
            Ring ring = Rings[Selected[owner]];
            GameObject[] segments = ring.Segments;
            for (int i = 0; i < segments.Length; i++)
            {
                HorizontalCoverage.Sample(Circles[owner], i, segments.Length, phase, out float x, out float z);
                Vector3 position = new(x, ring.Height, z);
                if (Physics.Raycast(position + Vector3.up * 500f, Vector3.down, out RaycastHit hit, 1000f, terrainMask))
                {
                    position.y = hit.point.y;
                    // ESP's offset prevents the Ashlands terrain from
                    // swallowing its projected line segments.
                    if (Heightmap.FindBiome(position) == Heightmap.Biome.AshLands) position.y += 0.5f;
                }
                segments[i].transform.position = position;
                segments[i].SetActive(!HorizontalCoverage.IsCovered(Circles, owner, x, z));
            }
            for (int i = 0; i < segments.Length; i++)
            {
                Vector3 before = segments[(i + segments.Length - 1) % segments.Length].transform.position;
                Vector3 after = segments[(i + 1) % segments.Length].transform.position;
                segments[i].transform.rotation = Quaternion.LookRotation((after - before).normalized, Vector3.up);
            }
        }
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
            .Boolean("enabled", Enabled).Integer("areas", Rings.Count));
    }

    private sealed class Ring
    {
        internal Ring(int count, float height) { Segments = new GameObject[count]; Height = height; }
        internal GameObject[] Segments { get; }
        internal float Height { get; set; }
        internal void Destroy()
        {
            foreach (GameObject segment in Segments)
                if (segment != null) { segment.SetActive(false); Object.Destroy(segment); }
        }
    }
}
