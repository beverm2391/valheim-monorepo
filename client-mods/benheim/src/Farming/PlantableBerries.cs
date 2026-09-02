using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Farming;

// PlantEverything 1.20.0 (AdvizeGH/Advize_ValheimMods at f50a18f, GPL-3.0)
// proved the product shape with these native prefabs. Benheim independently
// uses Valheim's public prefab, Piece, and PieceTable APIs here; no upstream
// code is copied.
internal static class PlantableBerries
{
    internal const int BerryCost = 5;

    private static readonly BerryDefinition[] Definitions =
    {
        new BerryDefinition("RaspberryBush", "Raspberry Bush", "Plant a native raspberry bush."),
        new BerryDefinition("BlueberryBush", "Blueberry Bush", "Plant a native blueberry bush."),
        new BerryDefinition("CloudberryBush", "Cloudberry Bush", "Plant a native cloudberry bush."),
    };

    private static readonly Dictionary<string, float> GridSpacingByPrefab =
        new Dictionary<string, float>(StringComparer.Ordinal);

    internal static bool IsBerryBush(GameObject prefab)
    {
        string prefabName = Utils.GetPrefabName(prefab);
        foreach (BerryDefinition definition in Definitions)
        {
            if (prefabName == definition.PrefabName)
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryGetGridSpacing(GameObject prefab, out float spacing)
    {
        return GridSpacingByPrefab.TryGetValue(Utils.GetPrefabName(prefab), out spacing);
    }

    /// <summary>
    /// Identifies an owned berry-bush placement before Piece.SetCreator assigns
    /// the local player as creator. It excludes natural bushes and pieces that
    /// already have a creator.
    /// </summary>
    internal static bool IsNewOwnedBerryPlacement(Piece piece)
    {
        ZNetView? netView = piece.GetComponent<ZNetView>();
        return IsBerryBush(piece.gameObject)
            && piece.GetCreator() == 0L
            && netView
            && netView.IsValid()
            && netView.IsOwner();
    }

    /// <summary>
    /// Uses Pickable's own replicated picked-state RPC after native placement
    /// establishes creator ownership. Pickable then owns the persisted picked
    /// timestamp and its ordinary respawn transition.
    /// </summary>
    internal static void StartPlacedBerryEmpty(Piece piece, bool wasNewOwnedBerryPlacement)
    {
        if (!wasNewOwnedBerryPlacement
            || piece.GetCreator() == 0L
            || !IsBerryBush(piece.gameObject))
        {
            return;
        }

        ZNetView? netView = piece.GetComponent<ZNetView>();
        if (!netView || !netView.IsValid() || !netView.IsOwner())
        {
            return;
        }

        netView.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", true);
    }

    internal static void TryRegister(ZNetScene scene)
    {
        try
        {
            Register(scene);
        }
        catch (Exception exception)
        {
            Plugin.Log.LogError($"Plantable berries are unavailable: {exception}");
            Diagnostics.Event(
                "Farming",
                "plantable_berries_registration_failed",
                $"error={Diagnostics.Flatten(exception.Message)}");
        }
    }

    private static void Register(ZNetScene scene)
    {
        ObjectDB objectDb = ObjectDB.instance
            ?? throw new InvalidOperationException("ObjectDB was not ready when ZNetScene registered prefabs.");
        GameObject cultivator = objectDb.GetItemPrefab("Cultivator")
            ?? throw new InvalidOperationException("The native Cultivator item prefab is missing.");
        ItemDrop cultivatorDrop = cultivator.GetComponent<ItemDrop>()
            ?? throw new InvalidOperationException("The native Cultivator has no ItemDrop component.");
        PieceTable pieceTable = cultivatorDrop.m_itemData.m_shared.m_buildPieces
            ?? throw new InvalidOperationException("The native Cultivator has no PieceTable.");
        EffectList placeEffect = FindNativePlantPlaceEffect(pieceTable);

        var prepared = new List<PreparedBerry>(Definitions.Length);
        foreach (BerryDefinition definition in Definitions)
        {
            GameObject prefab = scene.GetPrefab(definition.PrefabName)
                ?? throw new InvalidOperationException($"The native {definition.PrefabName} prefab is missing.");
            if (!prefab.GetComponent<ZNetView>())
            {
                throw new InvalidOperationException($"The native {definition.PrefabName} has no ZNetView.");
            }

            if (!prefab.GetComponent<Destructible>())
            {
                throw new InvalidOperationException($"The native {definition.PrefabName} has no Destructible component.");
            }

            Pickable pickable = prefab.GetComponent<Pickable>()
                ?? throw new InvalidOperationException($"The native {definition.PrefabName} has no Pickable component.");
            ItemDrop berry = pickable.m_itemPrefab?.GetComponent<ItemDrop>()
                ?? throw new InvalidOperationException($"The native {definition.PrefabName} has no berry ItemDrop.");
            float gridSpacing = ColliderFootprint(prefab);
            if (gridSpacing <= 0f)
            {
                throw new InvalidOperationException($"The native {definition.PrefabName} has no measurable collider footprint.");
            }

            prepared.Add(new PreparedBerry(definition, prefab, berry, gridSpacing));
        }

        foreach (PreparedBerry berry in prepared)
        {
            Piece piece = berry.Prefab.GetComponent<Piece>() ?? berry.Prefab.AddComponent<Piece>();
            ConfigurePiece(piece, berry, placeEffect);
            GridSpacingByPrefab[berry.Definition.PrefabName] = berry.GridSpacing;
            if (!pieceTable.m_pieces.Contains(berry.Prefab))
            {
                pieceTable.m_pieces.Add(berry.Prefab);
            }
        }

        Diagnostics.Event(
            "Farming",
            "plantable_berries_registered",
            $"count={prepared.Count} cost={BerryCost}");
    }

    private static EffectList FindNativePlantPlaceEffect(PieceTable pieceTable)
    {
        foreach (GameObject prefab in pieceTable.m_pieces)
        {
            if (!prefab)
            {
                continue;
            }

            Piece? piece = prefab.GetComponent<Piece>();
            if (piece && prefab.GetComponent<Plant>())
            {
                return piece.m_placeEffect;
            }
        }

        throw new InvalidOperationException("The native Cultivator has no plant Piece to supply placement effects.");
    }

    private static void ConfigurePiece(Piece piece, PreparedBerry berry, EffectList placeEffect)
    {
        piece.m_name = berry.Definition.DisplayName;
        piece.m_description = berry.Definition.Description;
        piece.m_icon = berry.ItemDrop.m_itemData.GetIcon();
        piece.m_category = Piece.PieceCategory.Misc;
        piece.m_groundPiece = true;
        piece.m_groundOnly = true;
        piece.m_cultivatedGroundOnly = false;
        piece.m_onlyInBiome = Heightmap.Biome.None;
        piece.m_canBeRemoved = false;
        piece.m_targetNonPlayerBuilt = false;
        piece.m_placeEffect = placeEffect;
        piece.m_resources = new[]
        {
            new Piece.Requirement
            {
                m_resItem = berry.ItemDrop,
                m_amount = BerryCost,
                m_recover = false,
            },
        };
    }

    private static float ColliderFootprint(GameObject prefab)
    {
        bool found = false;
        Bounds footprint = default;
        foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(includeInactive: true))
        {
            if (!collider || !collider.enabled || !TryGetLocalShapeBounds(collider, out Bounds shapeBounds))
            {
                continue;
            }

            Vector3 center = shapeBounds.center;
            Vector3 extents = shapeBounds.extents;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 localPoint = center + Vector3.Scale(
                            extents,
                            new Vector3(x, y, z));
                        Vector3 rootPoint = prefab.transform.InverseTransformPoint(
                            collider.transform.TransformPoint(localPoint));
                        if (!found)
                        {
                            footprint = new Bounds(rootPoint, Vector3.zero);
                            found = true;
                        }
                        else
                        {
                            footprint.Encapsulate(rootPoint);
                        }
                    }
                }
            }
        }

        return found ? Mathf.Max(footprint.size.x, footprint.size.z) : 0f;
    }

    private static bool TryGetLocalShapeBounds(Collider collider, out Bounds bounds)
    {
        if (collider is SphereCollider sphere)
        {
            float diameter = sphere.radius * 2f;
            bounds = new Bounds(sphere.center, new Vector3(diameter, diameter, diameter));
            return diameter > 0f;
        }

        if (collider is CapsuleCollider capsule)
        {
            float diameter = capsule.radius * 2f;
            Vector3 size = new Vector3(diameter, diameter, diameter);
            size[capsule.direction] = Mathf.Max(capsule.height, diameter);
            bounds = new Bounds(capsule.center, size);
            return diameter > 0f && size[capsule.direction] > 0f;
        }

        if (collider is BoxCollider box)
        {
            bounds = new Bounds(box.center, box.size);
            return box.size.x > 0f || box.size.y > 0f || box.size.z > 0f;
        }

        if (collider is MeshCollider mesh && mesh.sharedMesh)
        {
            bounds = mesh.sharedMesh.bounds;
            return bounds.size.x > 0f || bounds.size.y > 0f || bounds.size.z > 0f;
        }

        bounds = default;
        return false;
    }

    private sealed class BerryDefinition
    {
        internal BerryDefinition(string prefabName, string displayName, string description)
        {
            PrefabName = prefabName;
            DisplayName = displayName;
            Description = description;
        }

        internal string PrefabName { get; }
        internal string DisplayName { get; }
        internal string Description { get; }
    }

    private sealed class PreparedBerry
    {
        internal PreparedBerry(
            BerryDefinition definition,
            GameObject prefab,
            ItemDrop itemDrop,
            float gridSpacing)
        {
            Definition = definition;
            Prefab = prefab;
            ItemDrop = itemDrop;
            GridSpacing = gridSpacing;
        }

        internal BerryDefinition Definition { get; }
        internal GameObject Prefab { get; }
        internal ItemDrop ItemDrop { get; }
        internal float GridSpacing { get; }
    }
}

[HarmonyPatch(typeof(ZNetScene), "Awake")]
internal static class PlantableBerryRegistrationPatch
{
    [HarmonyPostfix]
    private static void Postfix(ZNetScene __instance)
    {
        PlantableBerries.TryRegister(__instance);
    }
}

[HarmonyPatch(typeof(Piece), nameof(Piece.SetCreator))]
internal static class PlantableBerryPlacementPatch
{
    [HarmonyPrefix]
    private static void Prefix(Piece __instance, out bool __state)
    {
        __state = PlantableBerries.IsNewOwnedBerryPlacement(__instance);
    }

    [HarmonyPostfix]
    private static void Postfix(Piece __instance, bool __state)
    {
        PlantableBerries.StartPlacedBerryEmpty(__instance, __state);
    }
}
