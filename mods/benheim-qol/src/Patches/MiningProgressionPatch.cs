using System;
using System.Collections;
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
    private const float MinAoeChance = 0.3f;
    private const float MaxAoeChance = 0.85f;
    private const float MaxAoeRadius = 3f;
    private const float AoeDamageMultiplier = 0.5f;
    private const int MaxAoeColliders = 24;
    private const int AoeHitsPerInterval = 8;
    private const float AoeSafetyResetSeconds = 10f;

    private static readonly Collider[] AoeColliders = new Collider[64];
    private static readonly List<Collider> AoeTargetColliders = new List<Collider>(MaxAoeColliders);
    private static readonly int AoeMask = LayerMask.GetMask("static_solid", "Default_small", "Default");

    private static bool aoeRunning;
    private static float aoeStartedAt;

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
        if (aoeRunning || !IsLocalPickaxeHit(hit))
        {
            return;
        }

        float skillFactor = GetPickaxeSkillFactor(hit);
        float multiplier = 1f + MaxPrimaryDamageBonus * skillFactor;
        if (RollCrit(skillFactor))
        {
            multiplier *= CritMultiplier;
            DamageText.instance?.ShowText(DamageText.TextType.Bonus, hit.m_point, "CRIT", player: true);
        }

        hit.m_damage.Modify(multiplier);
    }

    private static void TryApplyAoe(Component primaryTarget, HitData hit)
    {
        if (!IsLocalPickaxeHit(hit))
        {
            return;
        }

        if (aoeRunning)
        {
            if (aoeStartedAt + AoeSafetyResetSeconds < Time.realtimeSinceStartup)
            {
                ResetAoeState();
            }

            return;
        }

        float skillFactor = GetPickaxeSkillFactor(hit);
        if (skillFactor * 100f < AoeUnlockLevel)
        {
            return;
        }

        float unlockedFactor = Mathf.InverseLerp(AoeUnlockLevel / 100f, 1f, skillFactor);
        float chance = Mathf.Lerp(MinAoeChance, MaxAoeChance, unlockedFactor);
        if (UnityEngine.Random.value > chance)
        {
            return;
        }

        float radius = Mathf.Lerp(1.25f, MaxAoeRadius, unlockedFactor);
        int colliderCount = Physics.OverlapSphereNonAlloc(hit.m_point, radius, AoeColliders, AoeMask);
        if (colliderCount == 0)
        {
            return;
        }

        AoeTargetColliders.Clear();
        for (int i = 0; i < colliderCount && AoeTargetColliders.Count < MaxAoeColliders; i++)
        {
            Collider collider = AoeColliders[i];
            if (!collider || collider == hit.m_hitCollider || !BelongsToTarget(primaryTarget, collider))
            {
                continue;
            }

            AoeTargetColliders.Add(collider);
        }

        if (AoeTargetColliders.Count == 0 || Player.m_localPlayer == null)
        {
            return;
        }

        HitData aoeHit = hit.Clone();
        aoeHit.m_damage.Modify(AoeDamageMultiplier);
        aoeHit.m_radius = 0f;

        aoeRunning = true;
        aoeStartedAt = Time.realtimeSinceStartup;
        DamageText.instance?.ShowText(DamageText.TextType.Bonus, hit.m_point + Vector3.up * 0.25f, "AOE", player: true);
        Player.m_localPlayer.StartCoroutine(ApplyAoeDamage(primaryTarget, AoeTargetColliders.ToArray(), aoeHit));
    }

    private static IEnumerator ApplyAoeDamage(Component primaryTarget, Collider[] targetColliders, HitData aoeHit)
    {
        int iterations = 0;
        try
        {
            for (int i = 0; i < targetColliders.Length; i++)
            {
                Collider collider = targetColliders[i];
                if (!collider)
                {
                    continue;
                }

                iterations++;
                if (iterations % AoeHitsPerInterval == 0)
                {
                    yield return new WaitForFixedUpdate();
                }

                aoeHit.m_hitCollider = collider;
                aoeHit.m_point = collider.bounds.center;
                if (primaryTarget is MineRock mineRock && collider.GetComponentInParent<MineRock>() == mineRock)
                {
                    mineRock.Damage(aoeHit);
                }
                else if (primaryTarget is MineRock5 mineRock5 && collider.GetComponentInParent<MineRock5>() == mineRock5)
                {
                    mineRock5.Damage(aoeHit);
                }
            }
        }
        finally
        {
            ResetAoeState();
        }
    }

    private static bool BelongsToTarget(Component primaryTarget, Collider collider)
    {
        if (primaryTarget is MineRock mineRock)
        {
            return collider.GetComponentInParent<MineRock>() == mineRock;
        }

        return primaryTarget is MineRock5 mineRock5 && collider.GetComponentInParent<MineRock5>() == mineRock5;
    }

    private static bool IsLocalPickaxeHit(HitData hit)
    {
        Player player = Player.m_localPlayer;
        return hit != null
            && player != null
            && hit.m_damage.m_pickaxe > 0f
            && hit.m_attacker == player.GetZDOID()
            && hit.CheckToolTier(0, alwaysAllowTierZero: true);
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

    private static void ResetAoeState()
    {
        aoeRunning = false;
        aoeStartedAt = 0f;
        AoeTargetColliders.Clear();
    }
}
