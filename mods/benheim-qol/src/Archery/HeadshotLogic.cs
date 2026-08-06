using System;
using BenheimQoL.Infrastructure;
using UnityEngine;

namespace BenheimQoL.Archery;

internal static class HeadshotLogic
{
    internal static void Apply(
        Projectile projectile,
        IDestructible destructible,
        Collider collider,
        Vector3 hitPoint,
        HitData hit)
    {
        if (projectile == null
            || destructible == null
            || collider == null
            || hit == null
            || projectile.m_aoe > 0f
            || (projectile.m_type & ProjectileType.Arrow) == 0
            || (projectile.m_type & ProjectileType.AOE) != 0
            || !hit.m_ranged
            || hit.m_skill != Skills.SkillType.Bows
            || hit.m_hitCollider != collider
            || hit.m_point != hitPoint)
        {
            return;
        }

        if (!(destructible is Character target)
            || target is Player
            || target.IsPlayer())
        {
            return;
        }

        Character attacker = hit.GetAttacker();
        if (!(attacker is Player))
        {
            return;
        }

        if (IsNativeWeakSpot(target, collider))
        {
            Diagnostics.Event("Headshots", "skipped", "reason=native_weak_spot");
            return;
        }

        if (!TryGetHeadQualification(target, collider, hitPoint, out float tolerance))
        {
            return;
        }

        float distance = Vector3.Distance(projectile.m_startPoint, hitPoint);
        if (float.IsNaN(distance) || float.IsInfinity(distance))
        {
            return;
        }

        float multiplier = HeadshotRules.DistanceMultiplier(distance);
        hit.m_damage.Modify(multiplier);
        // Native target-owner stagger is computed from modified physical /
        // lightning damage multiplied by this field. Inverting it preserves
        // the target's baseline stagger while allowing every damage component
        // to receive the headshot multiplier through normal native stacking.
        hit.m_staggerMultiplier = HeadshotRules.CompensatedStaggerMultiplier(
            hit.m_staggerMultiplier,
            multiplier);

        // This is collision-time local feedback. It does not assert that the
        // target owner accepted the hit or that any health changed.
        if (attacker == Player.m_localPlayer)
        {
            try
            {
                WorldFeedback.ShowAbove(
                    target.transform,
                    hitPoint - target.transform.position,
                    $"HEADSHOT · {Mathf.RoundToInt(distance)}m · ×{multiplier:0.00}");
            }
            catch (Exception exception)
            {
                Diagnostics.Event(
                    "Headshots",
                    "text_skipped",
                    $"reason={Diagnostics.Flatten(exception.Message)}");
            }
        }

        // Valheim already emits this same native critical effect for damage to
        // an already-staggering target. Do not layer a second copy on that
        // path; the target owner's ordinary RPC remains responsible for it.
        if (!target.IsStaggering())
        {
            try
            {
                target.m_critHitEffects.Create(
                    hitPoint,
                    Quaternion.identity,
                    target.transform);
            }
            catch (Exception exception)
            {
                Diagnostics.Event(
                    "Headshots",
                    "effect_skipped",
                    $"reason={Diagnostics.Flatten(exception.Message)}");
            }
        }

        Diagnostics.Event(
            "Headshots",
            "applied",
            $"distance_m={distance:0.##} multiplier={multiplier:0.00} tolerance={tolerance:0.###}");
    }

    private static bool IsNativeWeakSpot(Character target, Collider collider)
    {
        WeakSpot[] weakSpots = target.m_weakSpots;
        if (weakSpots == null)
        {
            return false;
        }

        for (int i = 0; i < weakSpots.Length; i++)
        {
            WeakSpot weakSpot = weakSpots[i];
            if (weakSpot != null && weakSpot.m_collider == collider)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetHeadQualification(
        Character target,
        Collider struckCollider,
        Vector3 hitPoint,
        out float tolerance)
    {
        tolerance = 0f;
        if (target.transform == null)
        {
            return false;
        }

        CapsuleCollider rootCollider = target.GetComponent<CapsuleCollider>();
        if (rootCollider == null)
        {
            return false;
        }

        Vector3 headPoint;
        try
        {
            headPoint = target.GetHeadPoint();
        }
        catch (Exception)
        {
            // Characters without an initialized Head bone are not eligible;
            // do not substitute a transform or a guessed world-space point.
            return false;
        }

        Bounds struckBounds = struckCollider.bounds;
        Vector3 rootScale = target.transform.lossyScale;
        float creatureScale = Mathf.Max(
            Mathf.Abs(rootScale.x),
            Mathf.Max(Mathf.Abs(rootScale.y), Mathf.Abs(rootScale.z)));
        float struckDiameter = Mathf.Max(struckBounds.size.x, struckBounds.size.z);
        tolerance = HeadshotRules.HeadTolerance(
            struckDiameter,
            rootCollider.radius * 2f,
            rootCollider.height,
            creatureScale);

        if (tolerance <= 0f)
        {
            return false;
        }

        float headDistance = Vector3.Distance(hitPoint, headPoint);
        return HeadshotRules.IsWithinTolerance(headDistance, tolerance);
    }
}
