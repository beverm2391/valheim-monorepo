using System;

namespace BenheimQoL.Archery;

/// <summary>
/// Pure headshot math. Keeping the distance and geometry ratios here makes the
/// gameplay rule testable without loading Unity or Valheim assemblies.
/// </summary>
internal static class HeadshotRules
{
    internal const float NearDistanceMeters = 20f;
    internal const float CapDistanceMeters = 60f;
    internal const float NearMultiplier = 1.25f;
    internal const float CapMultiplier = 1.50f;

    // The tolerance is a ratio of the actual struck/root collider dimensions.
    // It is never a fixed world-space radius: prefab scale changes the root
    // dimensions before the comparison, while a tiny struck collider cannot
    // silently grant a large head region.
    private const float MinimumRootSupportRatio = 0.12f;
    private const float MaximumRootRadiusRatio = 0.60f;
    private const float MaximumRootHeightRatio = 0.20f;

    internal static float DistanceMultiplier(float distanceMeters)
    {
        if (!IsFinite(distanceMeters) || distanceMeters <= NearDistanceMeters)
        {
            return NearMultiplier;
        }

        if (distanceMeters >= CapDistanceMeters)
        {
            return CapMultiplier;
        }

        float progress = (distanceMeters - NearDistanceMeters)
            / (CapDistanceMeters - NearDistanceMeters);
        return NearMultiplier + (CapMultiplier - NearMultiplier) * progress;
    }

    /// <summary>
    /// Computes a scale-relative radius around the animated head bone.
    /// struckDiameter is already in world units; rootDiameter/rootHeight are
    /// the root capsule's prefab-local dimensions and are scaled here.
    /// </summary>
    internal static float HeadTolerance(
        float struckDiameter,
        float rootDiameter,
        float rootHeight,
        float creatureScale,
        bool struckColliderContainsHead = false)
    {
        if (!IsFinite(struckDiameter)
            || !IsFinite(rootDiameter)
            || !IsFinite(rootHeight)
            || !IsFinite(creatureScale)
            || struckDiameter <= 0f
            || rootDiameter <= 0f
            || rootHeight <= 0f
            || creatureScale <= 0f)
        {
            return 0f;
        }

        float worldRootDiameter = rootDiameter * creatureScale;
        float worldRootHeight = rootHeight * creatureScale;
        float rootRadius = worldRootDiameter * 0.5f;
        float struckRadius = struckDiameter * 0.5f;

        // Use the collider actually struck as the primary support, provide a
        // small root-relative floor for segmented creature colliders, and cap
        // both by root width/height ratios so a broad collider cannot turn the
        // whole creature into a headshot.
        float collisionSupport = Math.Max(
            struckRadius,
            rootRadius * MinimumRootSupportRatio);
        float rootBound = Math.Min(
            rootRadius * MaximumRootRadiusRatio,
            worldRootHeight * MaximumRootHeightRatio);
        float tolerance = Math.Min(collisionSupport, rootBound);
        if (struckColliderContainsHead)
        {
            // Some large creatures use a dedicated child capsule for the head
            // while their root capsule stays centered on the torso. When the
            // collider's real shape contains the animated Head point, let the
            // owning Character's root width replace the root-height cap. The
            // actual impact still has to fall inside this capped radius around
            // the Head point, so broad child colliders gain no extra support.
            tolerance = Math.Max(
                tolerance,
                rootRadius * MaximumRootRadiusRatio);
        }

        return tolerance;
    }

    internal static bool IsWithinTolerance(float headDistance, float tolerance)
    {
        return IsFinite(headDistance)
            && IsFinite(tolerance)
            && headDistance >= 0f
            && tolerance > 0f
            && headDistance <= tolerance;
    }

    internal static float CompensatedStaggerMultiplier(
        float originalMultiplier,
        float damageMultiplier)
    {
        if (!IsFinite(originalMultiplier)
            || !IsFinite(damageMultiplier)
            || damageMultiplier <= 0f)
        {
            return originalMultiplier;
        }

        return originalMultiplier / damageMultiplier;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
