using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace BenheimQoL.Patches;

[HarmonyPatch]
internal static class MiningProgressionPatch
{
    private const float MaxPrimaryDamageBonus = 0.75f;
    private const float CritUnlockLevel = 25f;
    private const float MaxCritChance = 0.15f;
    private const float CritMultiplier = 2f;
    private const float AoeUnlockLevel = 50f;
    private const float MaxAoeRadius = 3f;
    private const float AoeDamageMultiplier = 0.5f;
    private const int MaxAoeTargets = 12;

    private static readonly Collider[] AoeColliders = new Collider[64];
    private static readonly HashSet<Component> SeenAoeTargets = new HashSet<Component>();
    private static readonly List<AoeTarget> AoeTargets = new List<AoeTarget>(MaxAoeTargets);
    private static readonly int AoeMask = LayerMask.GetMask("piece", "Default", "static_solid", "Default_small", "terrain");

    private static bool applyingAoe;

    [HarmonyPatch(typeof(MineRock), "Damage")]
    private static class MineRockDamagePatch
    {
        private static void Prefix(MineRock __instance, HitData hit)
        {
            EnhancePrimaryHit(hit);
        }

        private static void Postfix(MineRock __instance, HitData hit)
        {
            TryApplyAoe(__instance, hit);
        }
    }

    [HarmonyPatch(typeof(MineRock5), "Damage")]
    private static class MineRock5DamagePatch
    {
        private static void Prefix(MineRock5 __instance, HitData hit)
        {
            EnhancePrimaryHit(hit);
        }

        private static void Postfix(MineRock5 __instance, HitData hit)
        {
            TryApplyAoe(__instance, hit);
        }
    }

    private static void EnhancePrimaryHit(HitData hit)
    {
        if (applyingAoe || !IsPickaxeHit(hit))
        {
            return;
        }

        float skillFactor = GetPickaxeSkillFactor(hit);
        float multiplier = 1f + MaxPrimaryDamageBonus * skillFactor;
        if (RollCrit(skillFactor))
        {
            multiplier *= CritMultiplier;
        }

        hit.m_damage.Modify(multiplier);
    }

    private static void TryApplyAoe(Component primaryTarget, HitData hit)
    {
        if (applyingAoe || !IsPickaxeHit(hit))
        {
            return;
        }

        float skillFactor = GetPickaxeSkillFactor(hit);
        if (skillFactor * 100f < AoeUnlockLevel)
        {
            return;
        }

        float unlockedFactor = Mathf.InverseLerp(AoeUnlockLevel / 100f, 1f, skillFactor);
        float radius = Mathf.Lerp(1.25f, MaxAoeRadius, unlockedFactor);
        int colliderCount = Physics.OverlapSphereNonAlloc(hit.m_point, radius, AoeColliders, AoeMask);
        if (colliderCount == 0)
        {
            return;
        }

        SeenAoeTargets.Clear();
        AoeTargets.Clear();
        Transform primaryRoot = primaryTarget.transform.root;
        for (int i = 0; i < colliderCount && AoeTargets.Count < MaxAoeTargets; i++)
        {
            Collider collider = AoeColliders[i];
            if (!collider || collider.transform.root == primaryRoot)
            {
                continue;
            }

            MineRock mineRock = collider.GetComponentInParent<MineRock>();
            if (mineRock && SeenAoeTargets.Add(mineRock))
            {
                AoeTargets.Add(new AoeTarget(mineRock, collider));
                continue;
            }

            MineRock5 mineRock5 = collider.GetComponentInParent<MineRock5>();
            if (mineRock5 && SeenAoeTargets.Add(mineRock5))
            {
                AoeTargets.Add(new AoeTarget(mineRock5, collider));
            }
        }

        if (AoeTargets.Count == 0)
        {
            return;
        }

        HitData aoeHit = hit.Clone();
        aoeHit.m_damage.Modify(AoeDamageMultiplier);
        aoeHit.m_radius = 0.35f;

        applyingAoe = true;
        try
        {
            foreach (AoeTarget target in AoeTargets)
            {
                aoeHit.m_hitCollider = target.Collider;
                aoeHit.m_point = target.Collider.bounds.center;
                if (target.Component is MineRock mineRock)
                {
                    mineRock.Damage(aoeHit);
                }
                else if (target.Component is MineRock5 mineRock5)
                {
                    mineRock5.Damage(aoeHit);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Mining AOE failed: {ex.Message}");
        }
        finally
        {
            applyingAoe = false;
            SeenAoeTargets.Clear();
            AoeTargets.Clear();
        }
    }

    private static bool IsPickaxeHit(HitData hit)
    {
        return hit != null && hit.m_damage.m_pickaxe > 0f && hit.CheckToolTier(0, alwaysAllowTierZero: true);
    }

    private static float GetPickaxeSkillFactor(HitData hit)
    {
        Character attacker = hit.GetAttacker();
        if (attacker is Player player)
        {
            return player.GetSkillFactor(Skills.SkillType.Pickaxes);
        }

        return Player.m_localPlayer ? Player.m_localPlayer.GetSkillFactor(Skills.SkillType.Pickaxes) : 0f;
    }

    private static bool RollCrit(float skillFactor)
    {
        if (skillFactor * 100f < CritUnlockLevel)
        {
            return false;
        }

        float unlockedFactor = Mathf.InverseLerp(CritUnlockLevel / 100f, 1f, skillFactor);
        return UnityEngine.Random.value < MaxCritChance * unlockedFactor;
    }

    private readonly struct AoeTarget
    {
        internal AoeTarget(Component component, Collider collider)
        {
            Component = component;
            Collider = collider;
        }

        internal Component Component { get; }

        internal Collider Collider { get; }
    }
}
