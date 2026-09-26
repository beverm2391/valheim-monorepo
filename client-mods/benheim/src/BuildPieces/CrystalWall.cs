using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.BuildPieces;

// BuildPiecesCustomized 1.3.1 (shudnal/BuildPiecesCustomized at 1637e8d,
// Unlicense) demonstrates changing native Piece and WearNTear properties on
// the prefab and placed instances. Benheim only changes the crystal wall.
internal static class CrystalWall
{
    internal const string PrefabName = "crystal_wall_1x1";
    private const string BottomSnapName = "_BenheimCrystalBottom";
    private const string TopSnapName = "_BenheimCrystalTop";

    internal static bool IsCrystalWall(Piece? piece)
    {
        return piece && Utils.GetPrefabName(piece.gameObject) == PrefabName;
    }

    internal static void Configure(Piece piece)
    {
        if (!IsCrystalWall(piece))
        {
            return;
        }

        // Valheim's placement and structural code both consult this field.
        // Leave material strength and support wear native, so an unsupported
        // stack still gets Valheim's ordinary color and collapse behavior.
        WearNTear? wear = piece.GetComponent<WearNTear>();
        if (wear)
        {
            wear.m_supports = true;
        }

        if (!TryGetLocalColliderBounds(piece, out Bounds bounds))
        {
            return;
        }

        Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        Vector3 top = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        AddSnapPointIfMissing(piece, BottomSnapName, bottom);
        AddSnapPointIfMissing(piece, TopSnapName, top);
    }

    internal static void ReportSetup(ZNetScene scene)
    {
        GameObject? prefab = scene.GetPrefab(PrefabName);
        Piece? piece = prefab ? prefab.GetComponent<Piece>() : null;
        if (piece)
        {
            Configure(piece);
        }

        WearNTear? wear = piece ? piece.GetComponent<WearNTear>() : null;
        Bounds bounds = default;
        bool hasBounds = piece && TryGetLocalColliderBounds(piece, out bounds);
        int snapCount = piece ? SnapPointCount(piece) : 0;
        bool ready = wear && wear.m_supports && hasBounds && snapCount >= 2;
        if (!ready)
        {
            Plugin.Log.LogError("Crystal wall support setup failed: native prefab, WearNTear, collider, or snap points missing.");
        }

        Emit(DiagnosticEvent.Create("BuildPieces", "crystal_wall_setup")
            .Boolean("ready", ready)
            .Boolean("prefab_found", prefab)
            .Boolean("piece_found", piece)
            .Boolean("wear_found", wear)
            .Boolean("has_collider_bounds", hasBounds)
            .Number("bottom_y", bounds.min.y)
            .Number("top_y", bounds.max.y)
            .Integer("snap_points", snapCount));
    }

    internal static void ReportPlacement(Player player, Piece piece, bool placed)
    {
        if (!IsCrystalWall(piece))
        {
            return;
        }

        Emit(DiagnosticEvent.Create("BuildPieces", "crystal_wall_placement")
            .Boolean("placed", placed)
            .String("native_status", player.GetPlacementStatus().ToString()));
    }

    private static void Emit(DiagnosticEvent evidence)
    {
        try { Diagnostics.Emit(evidence); }
        catch (Exception exception)
        {
            // Diagnostics must never make a native placement fail.
            Plugin.Log.LogWarning($"Crystal wall diagnostics failed: {exception}");
        }
    }

    private static int SnapPointCount(Piece piece)
    {
        var points = new List<Transform>();
        piece.GetSnapPoints(points);
        return points.Count;
    }

    private static void AddSnapPointIfMissing(Piece piece, string name, Vector3 position)
    {
        var points = new List<Transform>();
        piece.GetSnapPoints(points);
        foreach (Transform point in points)
        {
            if (Vector3.Distance(point.localPosition, position) < 0.08f)
            {
                return;
            }
        }

        GameObject marker = new GameObject(name);
        marker.tag = "snappoint";
        marker.transform.SetParent(piece.transform, false);
        marker.transform.localPosition = position;
    }

    private static bool TryGetLocalColliderBounds(Piece piece, out Bounds bounds)
    {
        bool found = false;
        bounds = default;
        foreach (Collider collider in piece.GetComponentsInChildren<Collider>(includeInactive: true))
        {
            if (!collider || !collider.enabled || collider.isTrigger ||
                !TryGetShapeBounds(collider, out Bounds shape))
            {
                continue;
            }

            // Collider.bounds is empty for inactive prefabs. Project the local
            // shape through its hierarchy instead, so the prefab and instances
            // receive identical snap positions before a world is entered.
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = shape.center + Vector3.Scale(shape.extents, new Vector3(x, y, z));
                Vector3 local = piece.transform.InverseTransformPoint(collider.transform.TransformPoint(corner));
                if (!found)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        return found && bounds.size.y > 0f;
    }

    private static bool TryGetShapeBounds(Collider collider, out Bounds bounds)
    {
        if (collider is BoxCollider box)
        {
            bounds = new Bounds(box.center, box.size);
            return true;
        }
        if (collider is MeshCollider mesh && mesh.sharedMesh)
        {
            bounds = mesh.sharedMesh.bounds;
            return true;
        }
        if (collider is CapsuleCollider capsule)
        {
            float diameter = capsule.radius * 2f;
            Vector3 size = new Vector3(diameter, diameter, diameter);
            size[capsule.direction] = Mathf.Max(capsule.height, diameter);
            bounds = new Bounds(capsule.center, size);
            return true;
        }

        bounds = default;
        return false;
    }
}

[HarmonyPatch(typeof(Piece), "Awake")]
internal static class CrystalWallInstancePatch
{
    [HarmonyPostfix]
    private static void Postfix(Piece __instance) => CrystalWall.Configure(__instance);
}

[HarmonyPatch(typeof(ZNetScene), "Awake")]
internal static class CrystalWallPrefabPatch
{
    [HarmonyPostfix]
    private static void Postfix(ZNetScene __instance) => CrystalWall.ReportSetup(__instance);
}

[HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
internal static class CrystalWallPlacementPatch
{
    [HarmonyPostfix]
    private static void Postfix(Player __instance, Piece piece, bool __result) =>
        CrystalWall.ReportPlacement(__instance, piece, __result);
}
