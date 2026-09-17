using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Farming;

// TerrainOp sends only its prefab identity to the terrain owner. The optional
// trailing marker lets compatible Benheim peers reconstruct a scaled copy of
// the native settings without mutating ObjectDB's shared prefab settings. An
// older receiver ignores the trailing bytes and safely performs the native op.
internal static class HoeRadiusExpansion
{
    internal const float RadiusMultiplier = 3f;
    internal const int ProtocolVersion = 1;
    private const int ProtocolMagic = 0x42485231; // BHR1

    private static readonly ConditionalWeakTable<TerrainOp.Settings, OperationMarker> Outgoing = new();
    private static readonly ConditionalWeakTable<TerrainOp.Settings, OperationMarker> Incoming = new();
    private static readonly MethodInfo MemberwiseCloneMethod =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(object).FullName, "MemberwiseClone");

    internal static bool TryPrepareOperation(TerrainOp operation)
    {
        if (TerrainOp.m_forceDisableTerrainOps
            || operation == null
            || operation.m_settings == null
            || !InputState.IsLeftShiftHeld())
        {
            return false;
        }

        Player? player = Player.m_localPlayer;
        if (player == null || !IsSelectedHoeTerrainAction(player, operation.gameObject))
        {
            return false;
        }

        float nativeRadius = operation.m_settings.GetRadius();
        if (nativeRadius <= 0f)
        {
            Emit(DiagnosticEvent.Create("Farming", "hoe_radius_action_requested")
                .String("result", "blocked")
                .String("reason", "terrain_radius_unavailable")
                .String("terrain_action", PrefabName(operation.gameObject)));
            return false;
        }

        string operationId = Guid.NewGuid().ToString("N");
        Scale(operation.m_settings);
        var marker = new OperationMarker(operationId, nativeRadius, operation.m_settings.GetRadius());
        Outgoing.Add(operation.m_settings, marker);
        Emit(DiagnosticEvent.Create("Farming", "hoe_radius_action_requested")
            .String("result", "expanded")
            .String("reason", "left_shift")
            .String("operation_id", operationId)
            .String("terrain_action", PrefabName(operation.gameObject))
            .Integer("protocol_version", ProtocolVersion)
            .Number("native_radius", nativeRadius)
            .Number("expanded_radius", marker.ExpandedRadius)
            .Number("radius_multiplier", RadiusMultiplier));
        return true;
    }

    internal static bool IsSelectedHoeTerrainAction(Player player, GameObject? operationObject = null)
    {
        ItemDrop.ItemData? tool = FarmingReflection.GetRightItem(player);
        if (tool?.m_dropPrefab == null || tool.m_dropPrefab.name != "Hoe" || !player.InPlaceMode())
        {
            return false;
        }

        Piece? selected = player.GetSelectedPiece();
        if (selected == null || selected.gameObject.GetComponent<TerrainOp>() == null)
        {
            return false;
        }

        return operationObject == null
            || PrefabName(selected.gameObject) == PrefabName(operationObject);
    }

    internal static bool TryWriteProtocol(TerrainOp.Settings settings, ZPackage package)
    {
        if (!Outgoing.TryGetValue(settings, out OperationMarker? marker))
        {
            return false;
        }

        package.Write(ProtocolMagic);
        package.Write(ProtocolVersion);
        package.Write(marker.OperationId);
        return true;
    }

    internal static bool TryReadProtocol(ZPackage package, ref TerrainOp.Settings? settings)
    {
        if (settings == null || package.Size() == package.GetPos())
        {
            return false;
        }

        int start = package.GetPos();
        try
        {
            if (package.Size() - start < sizeof(int) * 2 + 1
                || package.ReadInt() != ProtocolMagic
                || package.ReadInt() != ProtocolVersion)
            {
                package.SetPos(start);
                return false;
            }

            string operationId = package.ReadString();
            if (package.GetPos() != package.Size()
                || !Guid.TryParseExact(operationId, "N", out _))
            {
                package.SetPos(start);
                return false;
            }

            float nativeRadius = settings.GetRadius();
            TerrainOp.Settings expanded = Clone(settings);
            Scale(expanded);
            var marker = new OperationMarker(operationId, nativeRadius, expanded.GetRadius());
            settings = expanded;
            Incoming.Add(expanded, marker);
            Emit(DiagnosticEvent.Create("Farming", "hoe_radius_action_received")
                .String("result", "expanded")
                .String("reason", "compatible_protocol")
                .String("operation_id", operationId)
                .Integer("protocol_version", ProtocolVersion)
                .Number("native_radius", nativeRadius)
                .Number("expanded_radius", marker.ExpandedRadius)
                .Number("radius_multiplier", RadiusMultiplier));
            return true;
        }
        catch (Exception exception)
        {
            package.SetPos(start);
            Emit(DiagnosticEvent.Create("Farming", "hoe_radius_action_received")
                .String("result", "failed")
                .String("reason", exception.GetType().Name)
                .Integer("protocol_version", ProtocolVersion));
            return false;
        }
    }

    internal static void RecordApplied(TerrainOp.Settings settings)
    {
        if (!Incoming.TryGetValue(settings, out OperationMarker? marker))
        {
            return;
        }

        Incoming.Remove(settings);
        Emit(DiagnosticEvent.Create("Farming", "hoe_radius_action_finished")
            .String("result", "applied")
            .String("reason", "native_terrain_operation")
            .String("operation_id", marker.OperationId)
            .Integer("protocol_version", ProtocolVersion)
            .Number("native_radius", marker.NativeRadius)
            .Number("expanded_radius", marker.ExpandedRadius)
            .Number("radius_multiplier", RadiusMultiplier));
    }

    private static TerrainOp.Settings Clone(TerrainOp.Settings source)
    {
        return MemberwiseCloneMethod.Invoke(source, null) as TerrainOp.Settings
            ?? throw new InvalidOperationException("Could not clone native terrain settings.");
    }

    private static void Scale(TerrainOp.Settings settings)
    {
        if (settings.m_level) settings.m_levelRadius *= RadiusMultiplier;
        if (settings.m_raise) settings.m_raiseRadius *= RadiusMultiplier;
        if (settings.m_smooth) settings.m_smoothRadius *= RadiusMultiplier;
        if (settings.m_paintCleared) settings.m_paintRadius *= RadiusMultiplier;
    }

    private static string PrefabName(GameObject value) => Utils.GetPrefabName(value.name);

    private static void Emit(DiagnosticEvent record)
    {
        try { Diagnostics.Emit(record); }
        catch (Exception) { }
    }

    private sealed class OperationMarker
    {
        internal OperationMarker(string operationId, float nativeRadius, float expandedRadius)
        {
            OperationId = operationId;
            NativeRadius = nativeRadius;
            ExpandedRadius = expandedRadius;
        }

        internal string OperationId { get; }
        internal float NativeRadius { get; }
        internal float ExpandedRadius { get; }
    }
}
