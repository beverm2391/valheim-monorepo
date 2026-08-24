using BenheimQoL.Infrastructure;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace BenheimQoL.EnemyTiers;

internal static class BoarTierGeometryDiagnostics
{
    private const string BoarPrefabName = "Boar";
    private const float InsideTolerance = 0.001f;

    // LevelEffects can apply the same level from both Start and OnLevelSet.
    // Keep this diagnostic-only state in memory so each Boar reports geometry
    // once per tier without adding saved identity or polling.
    private static readonly ConditionalWeakTable<Character, BoarTierObservationState> GeometryObservations = new();

    // One local-player melee sample per starred tier is enough to compare the
    // authored damage contact with the capsule, visible body, and head. This
    // resets with the client session and never participates in gameplay.
    private static readonly BoarTierObservationState SessionHitObservations = new();

    internal static DiagnosticEvent? ObserveAppliedProfile(LevelEffects levelEffects, string source)
    {
        Character? character = levelEffects.GetComponentInParent<Character>();
        if (!TryGetProfile(character, out int level, out BoarTierPhysicalProfile profile) ||
            character == null ||
            !BoarTierIdentity.HasAppliedProfile(levelEffects))
        {
            return null;
        }

        BoarTierObservationState state = GeometryObservations.GetOrCreateValue(character);
        if (state.HasGeometry(level))
        {
            return null;
        }

        CapsuleCollider? capsule = character.GetCollider();
        DiagnosticEvent diagnosticEvent = DiagnosticEvent.Create("EnemyTiers", "boar_tier_geometry")
            .String("source", source)
            .String("creature", BoarPrefabName)
            .String("creature_id", character.GetZDOID().ToString())
            .Integer("level", level)
            .Integer("stars", level - 1)
            .Number("applied_scale", profile.VisualScale)
            .Boolean("owner", character.IsOwner())
            .Boolean("tamed", character.IsTamed())
            .Boolean("capsule_available", capsule != null);

        AddVector(diagnosticEvent, "visual_local_scale", levelEffects.transform.localScale);
        AddVector(diagnosticEvent, "visual_world_scale", levelEffects.transform.lossyScale);

        if (capsule != null)
        {
            AddVector(diagnosticEvent, "capsule_center_local", capsule.center);
            diagnosticEvent
                .Number("capsule_radius", capsule.radius)
                .Number("capsule_height", capsule.height);
            AddBounds(diagnosticEvent, "capsule_bounds", capsule.bounds);
        }

        bool rendererAvailable = TryGetRendererBounds(character, out Bounds rendererBounds, out int rendererCount);
        diagnosticEvent
            .Boolean("renderer_bounds_available", rendererAvailable)
            .Integer("renderer_count", rendererCount);
        if (rendererAvailable)
        {
            AddBounds(diagnosticEvent, "renderer_bounds", rendererBounds);
        }

        bool headAvailable = TryGetHeadPoint(character, out Vector3 headPoint);
        diagnosticEvent.Boolean("head_available", headAvailable);
        if (headAvailable)
        {
            AddVector(diagnosticEvent, "head_position", headPoint);
            AddVector(diagnosticEvent, "head_local", character.transform.InverseTransformPoint(headPoint));

            if (capsule != null)
            {
                float headToCapsule = Vector3.Distance(headPoint, capsule.ClosestPoint(headPoint));
                diagnosticEvent
                    .Number("head_to_capsule_m", headToCapsule)
                    .Boolean("head_inside_capsule", headToCapsule <= InsideTolerance);
            }

            if (rendererAvailable)
            {
                diagnosticEvent.Number(
                    "head_to_renderer_bounds_m",
                    Vector3.Distance(headPoint, rendererBounds.ClosestPoint(headPoint)));
            }
        }

        return state.TryMarkGeometry(level) ? diagnosticEvent : null;
    }

    internal static DiagnosticEvent? ObserveLocalPlayerMeleeHit(Character target, HitData hit)
    {
        if (!TryGetProfile(target, out int level, out BoarTierPhysicalProfile profile))
        {
            return null;
        }

        Player? localPlayer = Player.m_localPlayer;
        Character? attacker = hit.GetAttacker();
        if (!BoarTierHitObservationRules.ShouldObserve(
                BoarTierIdentity.HasAppliedProfile(target),
                localPlayer != null,
                attacker != null && attacker == localPlayer,
                hit.m_ranged,
                hit.m_hitCollider != null) ||
            SessionHitObservations.HasPlayerHit(level))
        {
            return null;
        }

        CapsuleCollider? capsule = target.GetCollider();
        Collider struckCollider = hit.m_hitCollider!;
        Vector3 hitPoint = hit.m_point;
        DiagnosticEvent diagnosticEvent = DiagnosticEvent.Create("EnemyTiers", "boar_tier_player_hit_geometry")
            .String("scope", "authored_damage_contact")
            .String("creature", BoarPrefabName)
            .String("creature_id", target.GetZDOID().ToString())
            .Integer("level", level)
            .Integer("stars", level - 1)
            .Number("applied_scale", profile.VisualScale)
            .String("skill", hit.m_skill.ToString())
            .String("hit_type", hit.m_hitType.ToString())
            .Number("attack_radius", hit.m_radius)
            .String("struck_collider", struckCollider.name)
            .String("struck_collider_type", struckCollider.GetType().Name)
            .Boolean("struck_root_capsule", ReferenceEquals(struckCollider, capsule));

        AddVector(diagnosticEvent, "hit_point", hitPoint);
        AddVector(diagnosticEvent, "hit_point_local", target.transform.InverseTransformPoint(hitPoint));
        AddVector(diagnosticEvent, "hit_direction", hit.m_dir);
        AddBounds(diagnosticEvent, "struck_bounds", struckCollider.bounds);
        diagnosticEvent.Number(
            "hit_to_struck_collider_m",
            Vector3.Distance(hitPoint, struckCollider.ClosestPoint(hitPoint)));

        if (capsule != null)
        {
            AddBounds(diagnosticEvent, "capsule_bounds", capsule.bounds);
            diagnosticEvent.Number(
                "hit_to_capsule_m",
                Vector3.Distance(hitPoint, capsule.ClosestPoint(hitPoint)));
        }

        if (TryGetHeadPoint(target, out Vector3 headPoint))
        {
            AddVector(diagnosticEvent, "head_position", headPoint);
            diagnosticEvent.Number("hit_to_head_m", Vector3.Distance(hitPoint, headPoint));
        }

        if (TryGetRendererBounds(target, out Bounds rendererBounds, out int rendererCount))
        {
            diagnosticEvent.Integer("renderer_count", rendererCount);
            AddBounds(diagnosticEvent, "renderer_bounds", rendererBounds);
            diagnosticEvent.Number(
                "hit_to_renderer_bounds_m",
                Vector3.Distance(hitPoint, rendererBounds.ClosestPoint(hitPoint)));
        }

        return SessionHitObservations.TryMarkPlayerHit(level) ? diagnosticEvent : null;
    }

    private static bool TryGetProfile(
        Character? character,
        out int level,
        out BoarTierPhysicalProfile profile)
    {
        level = character?.GetLevel() ?? 0;
        profile = default;
        return character != null &&
            Utils.GetPrefabName(character.gameObject) == BoarPrefabName &&
            BoarTierPhysicalProfile.TryForLevel(level, out profile);
    }

    private static bool TryGetRendererBounds(Character character, out Bounds bounds, out int count)
    {
        bounds = default;
        count = 0;
        foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(includeInactive: true))
        {
            if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (count == 0)
            {
                bounds = renderer.bounds;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
            count++;
        }
        return count > 0;
    }

    private static bool TryGetHeadPoint(Character character, out Vector3 headPoint)
    {
        try
        {
            headPoint = character.GetHeadPoint();
            return IsFinite(headPoint);
        }
        catch (NullReferenceException)
        {
            headPoint = default;
            return false;
        }
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static void AddBounds(DiagnosticEvent diagnosticEvent, string name, Bounds bounds)
    {
        AddVector(diagnosticEvent, $"{name}_center", bounds.center);
        AddVector(diagnosticEvent, $"{name}_size", bounds.size);
    }

    private static void AddVector(DiagnosticEvent diagnosticEvent, string name, Vector3 value)
    {
        diagnosticEvent
            .Number($"{name}_x", value.x)
            .Number($"{name}_y", value.y)
            .Number($"{name}_z", value.z);
    }
}
