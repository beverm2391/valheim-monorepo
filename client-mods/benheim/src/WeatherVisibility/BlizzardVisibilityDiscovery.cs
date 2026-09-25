using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.WeatherVisibility;

/// <summary>
/// Captures the loaded Mountain weather hierarchy that source inspection cannot
/// identify. The snapshot reads native objects and shared materials only. It
/// never changes the environment, enables or disables a component, or asks a
/// Renderer for its clone-producing material properties.
/// </summary>
internal static class BlizzardVisibilityDiscovery
{
    internal static void Run(string[] arguments, Action<string> output)
    {
        if (arguments.Length != 0)
        {
            output("Usage: bhrun blizzard");
            return;
        }

        string operationId = Diagnostics.NewOperationId();
        Player? player = Player.m_localPlayer;
        EnvMan? environmentManager = EnvMan.instance;
        EnvSetup? environment = environmentManager?.GetCurrentEnvironment();
        Heightmap.Biome environmentBiome = environmentManager != null
            ? environmentManager.GetCurrentBiome()
            : Heightmap.Biome.None;
        if (ZNetScene.instance == null || player == null || environmentManager == null ||
            environment == null || player.GetCurrentBiome() != Heightmap.Biome.Mountain ||
            environmentBiome != Heightmap.Biome.Mountain)
        {
            Diagnostics.Emit(Event("capture_unavailable", operationId)
                .String("reason", "requires_live_mountain_player_and_environment"));
            output("Blizzard capture needs a player in a loaded Mountain environment.");
            return;
        }

        GameObject[] particleRoots = environment.m_psystems ?? Array.Empty<GameObject>();
        Diagnostics.Emit(Event("capture_started", operationId)
            .String("environment", environment.m_name)
            .String("player_biome", player.GetCurrentBiome().ToString())
            .String("environment_biome", environmentBiome.ToString())
            .String("environment_object", ObjectIdentity(environment.m_envObject))
            .Integer("particle_root_count", particleRoots.Length)
            .Number("render_fog_density", RenderSettings.fogDensity)
            .Number("fog_density_night", environment.m_fogDensityNight)
            .Number("fog_density_morning", environment.m_fogDensityMorning)
            .Number("fog_density_day", environment.m_fogDensityDay)
            .Number("fog_density_evening", environment.m_fogDensityEvening));

        try
        {
            CaptureState state = new();
            if (environment.m_envObject != null)
            {
                CaptureRoot("environment_object", 0, environment.m_envObject.transform, operationId, state);
            }

            int writtenParticleRoots = BlizzardVisibilityCaptureLimits.WrittenRoots(
                particleRoots.Length);
            state.RootsTruncated |= particleRoots.Length > writtenParticleRoots;
            for (int index = 0; index < writtenParticleRoots; index++)
            {
                GameObject? root = particleRoots[index];
                if (root == null)
                {
                    Diagnostics.Emit(Event("asset_root_missing", operationId)
                        .String("role", "particle_root")
                        .Integer("root_index", index));
                    continue;
                }

                CaptureRoot("particle_root", index, root.transform, operationId, state);
            }

            Diagnostics.Emit(Event("capture_complete", operationId)
                .String("environment", environment.m_name)
                .Integer("root_count", state.RootCount)
                .Integer("node_count", state.NodeCount)
                .Integer("written_node_count", state.WrittenNodeCount)
                .Integer("particle_system_count", state.ParticleSystemCount)
                .Integer("mist_emitter_count", state.MistEmitterCount)
                .Boolean("roots_truncated", state.RootsTruncated)
                .Boolean("nodes_truncated", state.NodesTruncated)
                .Boolean("materials_truncated", state.MaterialsTruncated)
                .Boolean("mutation_attempted", false));

            output(
                $"Blizzard capture: environment={environment.m_name}, " +
                $"roots={state.RootCount}, particles={state.ParticleSystemCount}, " +
                $"mist_emitters={state.MistEmitterCount}" +
                $"{(state.AnyTruncated ? "; details capped" : string.Empty)}. " +
                "Evidence is in diagnostics.");
        }
        catch
        {
            Diagnostics.Emit(Event("capture_failed", operationId)
                .String("environment", environment.m_name)
                .Boolean("mutation_attempted", false));
            throw; // The shared snapshot dispatcher contains and reports failures.
        }
    }

    private static void CaptureRoot(
        string role,
        int rootIndex,
        Transform root,
        string operationId,
        CaptureState state)
    {
        Transform[] nodes = root.GetComponentsInChildren<Transform>(includeInactive: true);
        int writtenNodes = BlizzardVisibilityCaptureLimits.WrittenNodes(
            nodes.Length,
            state.WrittenNodeCount);
        state.RootCount++;
        state.NodeCount += nodes.Length;
        state.WrittenNodeCount += writtenNodes;
        state.NodesTruncated |= nodes.Length > writtenNodes;

        Diagnostics.Emit(Event("asset_root", operationId)
            .String("role", role)
            .Integer("root_index", rootIndex)
            .String("identity", ObjectIdentity(root.gameObject))
            .String("path", HierarchyPath(root))
            .Boolean("active_self", root.gameObject.activeSelf)
            .Boolean("active_in_hierarchy", root.gameObject.activeInHierarchy)
            .Integer("node_count", nodes.Length)
            .Integer("written_node_count", writtenNodes)
            .Boolean("nodes_truncated", nodes.Length > writtenNodes));

        for (int nodeIndex = 0; nodeIndex < writtenNodes; nodeIndex++)
        {
            Transform node = nodes[nodeIndex];
            ParticleSystem? particleSystem = node.GetComponent<ParticleSystem>();
            MistEmitter? mistEmitter = node.GetComponent<MistEmitter>();
            Renderer? renderer = node.GetComponent<Renderer>();
            Material[] materials = renderer != null
                ? renderer.sharedMaterials
                : Array.Empty<Material>();
            int writtenMaterials = BlizzardVisibilityCaptureLimits.WrittenMaterials(
                materials.Length);
            state.ParticleSystemCount += particleSystem != null ? 1 : 0;
            state.MistEmitterCount += mistEmitter != null ? 1 : 0;
            state.MaterialsTruncated |= materials.Length > writtenMaterials;

            List<string> materialIdentities = new(writtenMaterials);
            for (int materialIndex = 0; materialIndex < writtenMaterials; materialIndex++)
            {
                materialIdentities.Add(MaterialIdentity(materials[materialIndex]));
            }

            DiagnosticEvent record = Event("asset_node", operationId)
                .String("role", role)
                .Integer("root_index", rootIndex)
                .Integer("node_index", nodeIndex)
                .String("path", HierarchyPath(node))
                .String("components", ComponentNames(node))
                .Integer("layer", node.gameObject.layer)
                .Boolean("active_self", node.gameObject.activeSelf)
                .Boolean("active_in_hierarchy", node.gameObject.activeInHierarchy)
                .Boolean("particle_system_present", particleSystem != null)
                .Boolean("mist_emitter_present", mistEmitter != null)
                .Boolean("renderer_present", renderer != null)
                .String("renderer_type", renderer != null ? renderer.GetType().Name : null)
                .Boolean("renderer_enabled", renderer != null && renderer.enabled)
                .Integer("material_count", materials.Length)
                .String("materials", string.Join(",", materialIdentities))
                .Boolean("materials_truncated", materials.Length > writtenMaterials);

            if (particleSystem != null)
            {
                record
                    .Boolean("particle_emission_enabled", particleSystem.emission.enabled)
                    .Boolean("particle_playing", particleSystem.isPlaying)
                    .Integer("particle_count", particleSystem.particleCount);
            }
            if (mistEmitter != null)
            {
                record.Boolean("mist_emitter_enabled", mistEmitter.enabled);
            }
            Diagnostics.Emit(record);
        }
    }

    private static string ComponentNames(Component component)
    {
        Component[] components = component.GetComponents<Component>();
        int written = BlizzardVisibilityCaptureLimits.WrittenComponents(components.Length);
        List<string> names = new(written + (components.Length > written ? 1 : 0));
        for (int index = 0; index < written; index++)
        {
            names.Add(components[index] != null
                ? components[index].GetType().Name
                : "missing_script");
        }
        if (components.Length > written)
        {
            names.Add("...");
        }
        return string.Join(",", names);
    }

    private static string HierarchyPath(Transform transform)
    {
        List<string> parts = new();
        Transform? current = transform;
        for (int depth = 0; current != null && depth < 16; depth++, current = current.parent)
        {
            parts.Add(current.name + "[" + current.GetSiblingIndex() + "]");
        }
        if (current != null)
        {
            parts.Add("...");
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static string ObjectIdentity(UnityEngine.Object? value) =>
        value != null ? $"{value.name}|{value.GetType().Name}" : string.Empty;

    private static string MaterialIdentity(Material? material)
    {
        if (material == null)
        {
            return string.Empty;
        }

        string shader = material.shader != null ? material.shader.name : string.Empty;
        string texture = material.mainTexture != null
            ? $"{material.mainTexture.name}|{material.mainTexture.GetType().Name}"
            : string.Empty;
        return RuntimePrimitiveCatalogPolicy.StableMaterialIdentity(
            material.name,
            shader,
            texture);
    }

    private static DiagnosticEvent Event(string name, string operationId) =>
        DiagnosticEvent.Create("WeatherVisibility", name)
            .String("operation_id", operationId);

    private sealed class CaptureState
    {
        internal int RootCount { get; set; }
        internal int NodeCount { get; set; }
        internal int WrittenNodeCount { get; set; }
        internal int ParticleSystemCount { get; set; }
        internal int MistEmitterCount { get; set; }
        internal bool RootsTruncated { get; set; }
        internal bool NodesTruncated { get; set; }
        internal bool MaterialsTruncated { get; set; }
        internal bool AnyTruncated => RootsTruncated || NodesTruncated || MaterialsTruncated;
    }
}
