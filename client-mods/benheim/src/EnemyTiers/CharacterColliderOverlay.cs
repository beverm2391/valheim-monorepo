using BenheimQoL.Infrastructure;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BenheimQoL.EnemyTiers;

/// <summary>
/// Draws a local, transient wireframe around the live physics capsules owned by
/// nearby non-player Characters. The renderer never changes the colliders it
/// observes; all geometry is derived from their current native values.
/// </summary>
internal static class CharacterColliderOverlay
{
    private const string Usage = "bh debug colliders on|off";
    private const float MaximumDistance = 40f;
    private const float MaximumDistanceSquared = MaximumDistance * MaximumDistance;
    private const float LineWidth = 0.015f;
    private const int RingSegments = 24;
    private const int HemisphereSegments = 12;
    private static readonly Color LineColor = new(0.15f, 0.95f, 1f, 0.9f);
    private static readonly Dictionary<int, CapsuleWireframe> Wireframes = new();
    private static readonly List<int> WireframeIds = new();
    private static Material? lineMaterial;
    private static bool enabled;
    private static int scanGeneration;

    internal static bool TryExecute(string[] arguments, Terminal context)
    {
        if (arguments.Length != 4 ||
            !string.Equals(arguments[0], "bh", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(arguments[1], "debug", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(arguments[2], "colliders", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(arguments[3], "on", StringComparison.OrdinalIgnoreCase))
        {
            SetEnabled(true, context);
            return true;
        }

        if (string.Equals(arguments[3], "off", StringComparison.OrdinalIgnoreCase))
        {
            SetEnabled(false, context);
            return true;
        }

        context.AddString($"Usage: {Usage}");
        return true;
    }

    internal static void Update()
    {
        if (!enabled)
        {
            return;
        }

        Player? player = Player.m_localPlayer;
        if (player == null)
        {
            DisableAndDestroy();
            return;
        }

        scanGeneration++;
        Vector3 playerPosition = player.transform.position;
        List<Character> characters = Character.GetAllCharacters();
        for (int index = 0; index < characters.Count; index++)
        {
            Character character = characters[index];
            if (character == null ||
                !character.gameObject.activeInHierarchy ||
                character.IsPlayer() ||
                (character.transform.position - playerPosition).sqrMagnitude > MaximumDistanceSquared)
            {
                continue;
            }

            CapsuleCollider collider = character.GetCollider();
            if (collider == null ||
                !collider.enabled ||
                !collider.gameObject.activeInHierarchy ||
                collider.direction < 0 ||
                collider.direction > 2)
            {
                continue;
            }

            int id = character.GetInstanceID();
            if (!Wireframes.TryGetValue(id, out CapsuleWireframe wireframe))
            {
                wireframe = new CapsuleWireframe(id, collider, lineMaterial!);
                Wireframes.Add(id, wireframe);
                WireframeIds.Add(id);
            }
            else if (wireframe.Collider != collider)
            {
                wireframe.Destroy();
                wireframe = new CapsuleWireframe(id, collider, lineMaterial!);
                Wireframes[id] = wireframe;
            }

            wireframe.ScanGeneration = scanGeneration;
            wireframe.Refresh();
        }

        for (int index = WireframeIds.Count - 1; index >= 0; index--)
        {
            int id = WireframeIds[index];
            if (Wireframes.TryGetValue(id, out CapsuleWireframe wireframe) &&
                wireframe.ScanGeneration == scanGeneration)
            {
                continue;
            }

            if (wireframe != null)
            {
                wireframe.Destroy();
            }
            Wireframes.Remove(id);
            WireframeIds.RemoveAt(index);
        }
    }

    internal static void Reset()
    {
        DisableAndDestroy();
    }

    private static void SetEnabled(bool requestedState, Terminal context)
    {
        if (requestedState == enabled)
        {
            context.AddString($"Benheim collider overlay is already {(enabled ? "on" : "off")}.");
            return;
        }

        if (requestedState)
        {
            if (Player.m_localPlayer == null)
            {
                context.AddString("Benheim collider overlay is available only while playing in a world.");
                return;
            }

            if (!TryCreateMaterial())
            {
                context.AddString("Benheim collider overlay is unavailable: runtime line shader not found.");
                return;
            }

            enabled = true;
        }
        else
        {
            DisableAndDestroy();
        }

        Diagnostics.Emit(
            DiagnosticEvent.Create("EnemyTiers", "character_collider_overlay_toggled")
                .Boolean("enabled", enabled));
        context.AddString($"Benheim collider overlay {(enabled ? "on" : "off")}.");
    }

    private static bool TryCreateMaterial()
    {
        if (lineMaterial != null)
        {
            return true;
        }

        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
        {
            return false;
        }

        lineMaterial = new Material(shader)
        {
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = 5000,
        };
        lineMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        lineMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        lineMaterial.SetInt("_Cull", (int)CullMode.Off);
        lineMaterial.SetInt("_ZWrite", 0);
        lineMaterial.SetInt("_ZTest", (int)CompareFunction.Always);
        return true;
    }

    private static void DisableAndDestroy()
    {
        enabled = false;
        for (int index = 0; index < WireframeIds.Count; index++)
        {
            int id = WireframeIds[index];
            if (Wireframes.TryGetValue(id, out CapsuleWireframe wireframe))
            {
                wireframe.Destroy();
            }
        }
        Wireframes.Clear();
        WireframeIds.Clear();

        if (lineMaterial != null)
        {
            UnityEngine.Object.Destroy(lineMaterial);
            lineMaterial = null;
        }
    }

    private sealed class CapsuleWireframe
    {
        private readonly GameObject root;
        private readonly LineRenderer upperRing;
        private readonly LineRenderer lowerRing;
        private readonly LineRenderer firstProfile;
        private readonly LineRenderer secondProfile;

        internal CapsuleWireframe(int id, CapsuleCollider collider, Material material)
        {
            Collider = collider;
            root = new GameObject($"Benheim Character Collider {id}")
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            upperRing = CreateLine("Upper Ring", RingSegments, material);
            lowerRing = CreateLine("Lower Ring", RingSegments, material);
            firstProfile = CreateLine("First Profile", (HemisphereSegments + 1) * 2, material);
            secondProfile = CreateLine("Second Profile", (HemisphereSegments + 1) * 2, material);
        }

        internal CapsuleCollider Collider { get; }

        internal int ScanGeneration { get; set; }

        internal void Refresh()
        {
            Transform colliderTransform = Collider.transform;
            Vector3 scale = colliderTransform.lossyScale;
            Vector3 center = colliderTransform.TransformPoint(Collider.center);

            Vector3 axis;
            Vector3 firstRadial;
            Vector3 secondRadial;
            float axisScale;
            float radiusScale;
            switch (Collider.direction)
            {
                case 0:
                    axis = colliderTransform.right;
                    firstRadial = colliderTransform.up;
                    secondRadial = colliderTransform.forward;
                    axisScale = Mathf.Abs(scale.x);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                    break;
                case 2:
                    axis = colliderTransform.forward;
                    firstRadial = colliderTransform.right;
                    secondRadial = colliderTransform.up;
                    axisScale = Mathf.Abs(scale.z);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                    break;
                default:
                    axis = colliderTransform.up;
                    firstRadial = colliderTransform.right;
                    secondRadial = colliderTransform.forward;
                    axisScale = Mathf.Abs(scale.y);
                    radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    break;
            }

            float radius = Collider.radius * radiusScale;
            float height = Mathf.Max(Collider.height * axisScale, radius * 2f);
            float cylinderHalfLength = Mathf.Max(0f, (height * 0.5f) - radius);
            Vector3 upperCenter = center + (axis * cylinderHalfLength);
            Vector3 lowerCenter = center - (axis * cylinderHalfLength);

            SetRing(upperRing, upperCenter, firstRadial, secondRadial, radius);
            SetRing(lowerRing, lowerCenter, firstRadial, secondRadial, radius);
            SetProfile(firstProfile, center, axis, firstRadial, cylinderHalfLength, radius);
            SetProfile(secondProfile, center, axis, secondRadial, cylinderHalfLength, radius);
        }

        internal void Destroy()
        {
            UnityEngine.Object.Destroy(root);
        }

        private LineRenderer CreateLine(string name, int positionCount, Material material)
        {
            GameObject lineObject = new(name)
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            lineObject.transform.SetParent(root.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = positionCount;
            line.startWidth = LineWidth;
            line.endWidth = LineWidth;
            line.startColor = LineColor;
            line.endColor = LineColor;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;
            return line;
        }

        private static void SetRing(
            LineRenderer line,
            Vector3 center,
            Vector3 firstRadial,
            Vector3 secondRadial,
            float radius)
        {
            for (int index = 0; index < RingSegments; index++)
            {
                float angle = (Mathf.PI * 2f * index) / RingSegments;
                Vector3 point = center +
                    (firstRadial * (Mathf.Cos(angle) * radius)) +
                    (secondRadial * (Mathf.Sin(angle) * radius));
                line.SetPosition(index, point);
            }
        }

        private static void SetProfile(
            LineRenderer line,
            Vector3 center,
            Vector3 axis,
            Vector3 radial,
            float cylinderHalfLength,
            float radius)
        {
            Vector3 upperCenter = center + (axis * cylinderHalfLength);
            Vector3 lowerCenter = center - (axis * cylinderHalfLength);
            for (int index = 0; index <= HemisphereSegments; index++)
            {
                float angle = (Mathf.PI * index) / HemisphereSegments;
                line.SetPosition(
                    index,
                    upperCenter +
                        (radial * (Mathf.Cos(angle) * radius)) +
                        (axis * (Mathf.Sin(angle) * radius)));
            }

            int lowerStart = HemisphereSegments + 1;
            for (int index = 0; index <= HemisphereSegments; index++)
            {
                float angle = Mathf.PI + ((Mathf.PI * index) / HemisphereSegments);
                line.SetPosition(
                    lowerStart + index,
                    lowerCenter +
                        (radial * (Mathf.Cos(angle) * radius)) +
                        (axis * (Mathf.Sin(angle) * radius)));
            }
        }
    }
}
