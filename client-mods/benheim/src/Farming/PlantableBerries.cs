using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Farming;

// PlantEverything proved the product shape with these same native prefabs, but
// its implementation is GPL-3.0. Benheim independently uses Valheim's public
// prefab, Piece, and PieceTable APIs here; no PlantEverything code is copied.
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
            if (!collider)
            {
                continue;
            }

            Bounds bounds = collider.bounds;
            if (bounds.size.x <= 0f && bounds.size.z <= 0f)
            {
                continue;
            }

            if (!found)
            {
                footprint = bounds;
                found = true;
            }
            else
            {
                footprint.Encapsulate(bounds);
            }
        }

        return found ? Mathf.Max(footprint.size.x, footprint.size.z) : 0f;
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
