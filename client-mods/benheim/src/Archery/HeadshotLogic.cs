using System;
using BenheimQoL.CombatFeedback;
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
            || (projectile.m_type & ProjectileType.AOE) != 0)
        {
            return;
        }

        // Only log decisions for arrow collisions with a Character. Terrain,
        // props, and other projectile types are not headshot candidates and
        // would turn a useful trace into collision spam.
        if (!(destructible is Character target))
        {
            return;
        }

        if (target is Player || target.IsPlayer())
        {
            Skip("player_target", target, collider);
            return;
        }

        if (!hit.m_ranged)
        {
            Skip("not_ranged", target, collider);
            return;
        }

        if (hit.m_skill != Skills.SkillType.Bows)
        {
            Skip("skill_not_bows", target, collider, $"skill={hit.m_skill}");
            return;
        }

        if (hit.m_hitCollider != collider)
        {
            Skip("collider_mismatch", target, collider);
            return;
        }

        if (hit.m_point != hitPoint)
        {
            Skip("point_mismatch", target, collider);
            return;
        }

        Character attacker = hit.GetAttacker();
        if (!(attacker is Player))
        {
            Skip("attacker_not_player", target, collider);
            return;
        }

        if (IsNativeWeakSpot(target, collider))
        {
            Skip("native_weak_spot", target, collider);
            return;
        }

        if (!TryGetHeadQualification(
            target,
            collider,
            hitPoint,
            out float tolerance,
            out float headDistance,
            out bool directHeadCollider,
            out bool containsHead,
            out bool headCentered,
            out bool struckRootCollider,
            out bool struckTriggerCollider,
            out float headCenterDistance,
            out float headCenterLimit,
            out string qualificationReason))
        {
            Skip(
                qualificationReason,
                target,
                collider,
                $"head_distance_m={headDistance:0.###} tolerance={tolerance:0.###} "
                + $"qualification_path=fallback head_collider={Diagnostics.Bool(directHeadCollider)} "
                + $"contains_head={Diagnostics.Bool(containsHead)} "
                + $"head_centered={Diagnostics.Bool(headCentered)} "
                + $"root_collider={Diagnostics.Bool(struckRootCollider)} "
                + $"trigger_collider={Diagnostics.Bool(struckTriggerCollider)} "
                + $"head_center_distance_m={headCenterDistance:0.###} "
                + $"head_center_limit_m={headCenterLimit:0.###}");
            return;
        }

        float distance = Vector3.Distance(projectile.m_startPoint, hitPoint);
        if (float.IsNaN(distance) || float.IsInfinity(distance))
        {
            Skip("invalid_projectile_distance", target, collider);
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

            CombatFeedbackController.RequestShake(CombatFeedbackTrigger.Headshot);
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
            $"target={Describe(target)} collider={Describe(collider)} "
            + $"distance_m={distance:0.##} multiplier={multiplier:0.00} "
            + $"head_distance_m={headDistance:0.###} tolerance={tolerance:0.###} "
            + $"qualification_path={(directHeadCollider ? "head_collider" : "fallback")} "
            + $"head_collider={Diagnostics.Bool(directHeadCollider)} "
            + $"contains_head={Diagnostics.Bool(containsHead)} "
            + $"head_centered={Diagnostics.Bool(headCentered)} "
            + $"root_collider={Diagnostics.Bool(struckRootCollider)} "
            + $"trigger_collider={Diagnostics.Bool(struckTriggerCollider)} "
            + $"head_center_distance_m={headCenterDistance:0.###} "
            + $"head_center_limit_m={headCenterLimit:0.###}");
    }

    private static void Skip(
        string reason,
        Character target,
        Collider collider,
        string details = "")
    {
        string suffix = $"reason={reason} target={Describe(target)} collider={Describe(collider)}";
        if (!string.IsNullOrWhiteSpace(details))
        {
            suffix += $" {details}";
        }

        Diagnostics.Event("Headshots", "skipped", suffix);
    }

    private static string Describe(UnityEngine.Object value)
    {
        if (value == null)
        {
            return "none";
        }

        try
        {
            return Diagnostics.Flatten(value.name);
        }
        catch (Exception)
        {
            return "unknown";
        }
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
        out float tolerance,
        out float headDistance,
        out bool directHeadCollider,
        out bool containsHead,
        out bool headCentered,
        out bool struckRootCollider,
        out bool struckTriggerCollider,
        out float headCenterDistance,
        out float headCenterLimit,
        out string reason)
    {
        tolerance = 0f;
        headDistance = 0f;
        directHeadCollider = false;
        containsHead = false;
        headCentered = false;
        struckRootCollider = false;
        struckTriggerCollider = false;
        headCenterDistance = 0f;
        headCenterLimit = 0f;
        reason = "head_qualification_failed";
        if (target.transform == null)
        {
            reason = "target_transform_missing";
            return false;
        }

        CapsuleCollider rootCollider = target.GetComponent<CapsuleCollider>();
        if (rootCollider == null)
        {
            reason = "root_collider_missing";
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
            reason = "head_point_missing";
            return false;
        }

        Bounds struckBounds = struckCollider.bounds;
        Vector3 rootScale = target.transform.lossyScale;
        float creatureScale = Mathf.Max(
            Mathf.Abs(rootScale.x),
            Mathf.Max(Mathf.Abs(rootScale.y), Mathf.Abs(rootScale.z)));
        float struckDiameter = Mathf.Max(struckBounds.size.x, struckBounds.size.z);
        float containmentEpsilon = Mathf.Max(creatureScale * 0.001f, 0.0001f);
        Vector3 closestHeadPoint;
        try
        {
            // Collider.ClosestPoint is shape- and rotation-aware. Unity returns
            // the input point when it lies inside the collider, unlike the
            // world-axis-aligned Bounds check that can include empty corners.
            closestHeadPoint = struckCollider.ClosestPoint(headPoint);
        }
        catch (Exception)
        {
            reason = "head_collider_inspection_failed";
            return false;
        }

        // Collider.ClosestPoint returns its input point for a point inside the
        // real shape. Use that containment result directly: a near miss must
        // not enter the exact-volume path just because it is within a scale
        // epsilon of the collider surface.
        containsHead = closestHeadPoint.Equals(headPoint);
        struckRootCollider = struckCollider == rootCollider;
        struckTriggerCollider = struckCollider.isTrigger;
        headCenterDistance = Vector3.Distance(headPoint, struckBounds.center);
        headCenterLimit = Mathf.Min(
            struckBounds.extents.x,
            Mathf.Min(struckBounds.extents.y, struckBounds.extents.z));
        headCentered = HeadshotRules.IsHeadCenteredInBounds(
            headCenterDistance,
            headCenterLimit);
        directHeadCollider = HeadshotRules.IsDirectHeadCollider(
            isRootCollider: struckRootCollider,
            isTrigger: struckTriggerCollider,
            containsHead: containsHead,
            headCenterDistance: headCenterDistance,
            minimumBoundsExtent: headCenterLimit);

        // The old point-and-tolerance path intentionally remains in place for
        // every other collider. Keep its original scale-relative containment
        // input and conservative caps; neither is a proxy for the exact direct
        // head-volume decision above.
        bool fallbackContainsHead = !struckRootCollider
            && Vector3.Distance(closestHeadPoint, headPoint) <= containmentEpsilon;
        tolerance = HeadshotRules.HeadTolerance(
            struckDiameter,
            rootCollider.radius * 2f,
            rootCollider.height,
            creatureScale,
            fallbackContainsHead);

        headDistance = Vector3.Distance(hitPoint, headPoint);
        if (directHeadCollider)
        {
            return true;
        }

        if (tolerance <= 0f)
        {
            reason = "invalid_head_tolerance";
            return false;
        }

        if (!HeadshotRules.IsWithinTolerance(headDistance, tolerance))
        {
            reason = "outside_head_tolerance";
            return false;
        }

        return true;
    }
}
