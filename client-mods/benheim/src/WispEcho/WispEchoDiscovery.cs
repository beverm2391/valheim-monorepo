using System;
using System.Collections.Generic;
using BenheimQoL.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace BenheimQoL.WispEcho;

/// <summary>
/// The prerequisite for the single-hostile render experiment. Source inspection
/// cannot tell us the loaded mist material, depth state, or creature LOD layout.
/// This snapshot only reads those assets; it never instantiates a material,
/// changes a renderer, or retains a world object after the command returns.
/// </summary>
internal static class WispEchoDiscovery
{
    private const float Radius = 40f;
    private const int MaximumRenderers = 16;
    private const int MaximumMaterials = 4;
    private const int MaximumShaderProperties = 64;
    private static readonly string[] RenderStateProperties =
        { "_ZTest", "_ZWrite", "_SrcBlend", "_DstBlend", "_Cull" };

    internal static void Run(string[] arguments, Action<string> output)
    {
        if (arguments.Length != 0)
        {
            output("Usage: bhrun wispecho");
            return;
        }

        string operationId = Diagnostics.NewOperationId();
        Player? player = Player.m_localPlayer;
        Camera? camera = Utils.GetMainCamera();
        if (ZNetScene.instance == null || player == null || camera == null ||
            player.IsDead() || player.GetCurrentBiome() != Heightmap.Biome.Mistlands)
        {
            Diagnostics.Emit(Event("capture_unavailable", operationId)
                .String("reason", "requires_live_mistlands_player_and_camera"));
            output("Wisp Echo capture needs a living player in the Mistlands with a loaded camera.");
            return;
        }

        // Native loaded-character enumeration, also used by ProtectiveWards
        // WardOfferings.cs (095040e, Unlicense). The native hostility predicate
        // preserves aggravated Dvergr semantics; never infer threats from names.
        Character? target = null;
        float nearestSquared = Radius * Radius;
        foreach (Character character in Character.GetAllCharacters())
        {
            if (character == null || !character.gameObject.activeInHierarchy ||
                character.IsPlayer() || character.IsTamed() || character.IsDead() ||
                !BaseAI.IsEnemy(player, character))
            {
                continue;
            }
            float distanceSquared = (character.transform.position - player.transform.position).sqrMagnitude;
            if (distanceSquared < nearestSquared)
            {
                target = character;
                nearestSquared = distanceSquared;
            }
        }

        if (target == null)
        {
            Diagnostics.Emit(Event("capture_unavailable", operationId)
                .String("reason", "no_loaded_hostile_within_40m"));
            output("Wisp Echo capture: no loaded hostile within 40 m.");
            return;
        }

        Diagnostics.Emit(Event("capture_started", operationId)
            .String("target", target.name)
            .Integer("target_instance", target.GetInstanceID())
            .Number("distance", Mathf.Sqrt(nearestSquared))
            .Boolean("native_mist_blocks_target", ParticleMist.IsMistBlocked(
                camera.transform.position, target.GetCenterPoint())));
        try
        {
            Diagnostics.Emit(Event("camera", operationId)
                .String("path", HierarchyPath(camera.transform))
                .String("rendering_path", camera.actualRenderingPath.ToString())
                .String("depth_texture_mode", camera.depthTextureMode.ToString())
                .String("graphics_device", SystemInfo.graphicsDeviceType.ToString())
                .Boolean("reversed_z", SystemInfo.usesReversedZBuffer)
                .Boolean("target_texture", camera.targetTexture != null)
                .Integer("culling_mask", camera.cullingMask)
                .String("components", ComponentNames(camera)));

            bool truncated = CaptureRenderers("hostile", target.transform, camera, operationId);
            ParticleMist? mist = ParticleMist.instance;
            if (mist != null)
            {
                // NoFogBruh's separate Mistlands_Globalmist path (6ee805b, MIT)
                // is evidence that ordinary fog is insufficient. Use the native
                // singleton to observe its actual loaded hierarchy, not that mod's
                // hard-coded scene paths or mist-disabling behavior.
                ParticleSystem? particles = mist.GetComponent<ParticleSystem>();
                Diagnostics.Emit(Event("mist", operationId)
                    .String("path", HierarchyPath(mist.transform))
                    .Boolean("active", mist.isActiveAndEnabled)
                    .Boolean("particle_system_present", particles != null)
                    .Integer("particle_count", particles != null ? particles.particleCount : 0)
                    .Boolean("emission_enabled", particles != null && particles.emission.enabled));
                truncated |= CaptureRenderers("mist", mist.transform, camera, operationId);
            }

            // The collider overlay already uses this shader, but with ZTest Always.
            // Inspect its loaded contract without inheriting that wallhack state.
            // Availability and property names do not prove a correct cyan fill.
            Shader? shader = Shader.Find("Hidden/Internal-Colored");
            CaptureShader(shader, operationId);
            Diagnostics.Emit(Event("capture_complete", operationId)
                .Boolean("mist_present", mist != null)
                .Boolean("candidate_shader_supported", shader != null && shader.isSupported)
                .Boolean("truncated", truncated)
                .Boolean("render_attempted", false));
            output($"Wisp Echo captured {target.name}; mist {(mist != null ? "found" : "missing")}" +
                $"{(truncated ? "; details capped" : string.Empty)}. No reveal drawn; evidence is in diagnostics.");
        }
        catch
        {
            Diagnostics.Emit(Event("capture_failed", operationId));
            throw; // The existing snapshot dispatcher contains and reports failures.
        }
    }

    private static bool CaptureRenderers(string role, Transform root, Camera camera, string operationId)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactive: true);
        bool truncated = renderers.Length > MaximumRenderers;
        Diagnostics.Emit(Event("renderer_root", operationId)
            .String("role", role)
            .String("path", HierarchyPath(root))
            .Integer("renderer_count", renderers.Length)
            .Integer("written_count", Math.Min(renderers.Length, MaximumRenderers)));
        for (int index = 0; index < Math.Min(renderers.Length, MaximumRenderers); index++)
        {
            Renderer renderer = renderers[index];
            // sharedMaterials/sharedMesh are observation-only. Renderer.material(s)
            // would silently clone native materials even in this read-only probe.
            Material[] materials = renderer.sharedMaterials;
            SkinnedMeshRenderer? skin = renderer as SkinnedMeshRenderer;
            Mesh? mesh = skin != null ? skin.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
            Animator? animator = renderer.GetComponentInParent<Animator>();
            LODGroup? lod = renderer.GetComponentInParent<LODGroup>();
            Diagnostics.Emit(Event("renderer", operationId)
                .String("role", role)
                .Integer("renderer_index", index)
                .String("path", HierarchyPath(renderer.transform))
                .String("kind", renderer.GetType().Name)
                .String("components", ComponentNames(renderer))
                .Boolean("enabled", renderer.enabled)
                .Boolean("active", renderer.gameObject.activeInHierarchy)
                .Boolean("visible_any_camera", renderer.isVisible)
                .Boolean("camera_layer_included", (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0)
                .Boolean("force_rendering_off", renderer.forceRenderingOff)
                .Integer("sorting_layer", renderer.sortingLayerID)
                .Integer("sorting_order", renderer.sortingOrder)
                .Integer("material_count", materials.Length)
                .String("mesh", mesh != null ? mesh.name : null)
                .Integer("submesh_count", mesh != null ? mesh.subMeshCount : 0)
                .Boolean("vertex_colors", mesh != null && mesh.HasVertexAttribute(VertexAttribute.Color))
                .String("root_bone", skin != null && skin.rootBone != null ? HierarchyPath(skin.rootBone) : null)
                .Boolean("update_when_offscreen", skin != null && skin.updateWhenOffscreen)
                .String("animator", animator != null ? HierarchyPath(animator.transform) : null)
                .String("animation_culling", animator != null ? animator.cullingMode.ToString() : null)
                .String("lod_group", lod != null ? HierarchyPath(lod.transform) : null));

            truncated |= materials.Length > MaximumMaterials;
            for (int slot = 0; slot < Math.Min(materials.Length, MaximumMaterials); slot++)
            {
                Material? material = materials[slot];
                DiagnosticEvent entry = Event("material", operationId)
                    .String("role", role)
                    .Integer("renderer_index", index)
                    .Integer("slot", slot)
                    .Boolean("present", material != null);
                if (material != null)
                {
                    entry.String("name", material.name)
                        .String("shader", material.shader != null ? material.shader.name : null)
                        .Integer("render_queue", material.renderQueue)
                        .String("render_type", material.GetTag("RenderType", false, ""))
                        .String("queue_tag", material.GetTag("Queue", false, ""))
                        .Integer("pass_count", material.passCount);
                    foreach (string property in RenderStateProperties)
                    {
                        entry.Boolean(property + "_exposed", material.HasProperty(property));
                        if (material.HasProperty(property))
                        {
                            entry.Number(property, material.GetFloat(property));
                        }
                    }
                }
                Diagnostics.Emit(entry);
            }
        }
        return truncated;
    }

    private static void CaptureShader(Shader? shader, string operationId)
    {
        List<string> properties = new();
        int count = shader != null ? shader.GetPropertyCount() : 0;
        for (int index = 0; index < Math.Min(count, MaximumShaderProperties); index++)
        {
            properties.Add(shader!.GetPropertyName(index) + ":" + shader.GetPropertyType(index));
        }
        Diagnostics.Emit(Event("candidate_shader", operationId)
            .Boolean("present", shader != null)
            .String("shader", shader != null ? shader.name : null)
            .Boolean("supported", shader != null && shader.isSupported)
            .Integer("render_queue", shader != null ? shader.renderQueue : -1)
            .String("properties", string.Join(",", properties))
            .Boolean("properties_truncated", count > MaximumShaderProperties));
    }

    private static string ComponentNames(Component component)
    {
        Component[] components = component.GetComponents<Component>();
        List<string> names = new();
        for (int index = 0; index < Math.Min(components.Length, 16); index++)
        {
            names.Add(components[index] != null ? components[index].GetType().Name : "missing_script");
        }
        if (components.Length > 16)
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

    private static DiagnosticEvent Event(string name, string operationId) =>
        DiagnosticEvent.Create("WispEcho", name).String("operation_id", operationId);
}
