#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
logic="$root/src/Archery/HeadshotLogic.cs"
patch="$root/src/Archery/HeadshotPatches.cs"
rules="$root/src/Archery/HeadshotRules.cs"

# The hook is the shooter-side collision seam, before native Damage serializes
# the freshly-built HitData. It must fail closed if the pinned call shape drifts.
grep -Fq '[HarmonyPatch(typeof(Projectile), nameof(Projectile.OnHit))]' "$patch"
grep -Fq 'Expected exactly one direct Projectile damage call' "$patch"
grep -Fq 'Projectile damage seam locals changed' "$patch"
grep -Fq 'IDestructible.Damage' "$patch"
grep -Fq 'HeadshotLogic.Apply' "$patch"
grep -Fq 'projectile.m_startPoint' "$logic"
grep -Fq 'projectile.m_aoe > 0f' "$logic"
grep -Fq 'ProjectileType.Arrow' "$logic"

# Provenance and exact native weak-spot identity are checked before mutation.
grep -Fq 'hit.m_ranged' "$logic"
grep -Fq 'hit.m_skill != Skills.SkillType.Bows' "$logic"
grep -Fq 'hit.GetAttacker()' "$logic"
grep -Fq 'weakSpot.m_collider == collider' "$logic"
grep -Fq 'struckCollider.ClosestPoint(headPoint)' "$logic"
grep -Fq 'head_collider_inspection_failed' "$logic"
grep -Fq 'struckCollider.isTrigger' "$logic"
grep -Fq 'IsDirectHeadCollider' "$logic"
grep -Fq 'qualification_path=' "$logic"
grep -Fq 'contains_head=' "$logic"
grep -Fq 'head_center_distance_m=' "$logic"
grep -Fq 'head_center_limit_m=' "$logic"
grep -Fq 'root_collider=' "$logic"
grep -Fq 'trigger_collider=' "$logic"
grep -Fq 'containsHead = closestHeadPoint.Equals(headPoint);' "$logic"
grep -Fq 'bool fallbackContainsHead = !struckRootCollider' "$logic"
grep -Fq 'Vector3.Distance(closestHeadPoint, headPoint) <= containmentEpsilon;' "$logic"
if grep -Fq 'struckBounds.Contains(headPoint)' "$logic"; then
  printf 'head collider qualification must use the collider shape, not its AABB\n' >&2
  exit 1
fi
grep -Fq '"outside_head_tolerance"' "$logic"
grep -Fq '"head_point_missing"' "$logic"
grep -Fq '"skill_not_bows"' "$logic"

# Damage is modified once and native stagger remains at its baseline.
grep -Fq 'hit.m_damage.Modify(multiplier)' "$logic"
grep -Fq 'CompensatedStaggerMultiplier' "$logic"
grep -Fq 'target.m_critHitEffects.Create' "$logic"
grep -Fq 'if (!target.IsStaggering())' "$logic"
grep -Fq 'WorldFeedback.ShowAbove' "$logic"
grep -Fq 'Diagnostics.Event("Headshots"' "$logic"
grep -Fq 'HeadTolerance' "$rules"
grep -Fq 'minimumBoundsExtent' "$rules"

# Direct head volumes must not widen the established point-tolerance fallback.
grep -Fq 'private const float MinimumRootSupportRatio = 0.12f;' "$rules"
grep -Fq 'private const float MaximumRootRadiusRatio = 0.60f;' "$rules"
grep -Fq 'private const float MaximumRootHeightRatio = 0.20f;' "$rules"

# A direct head collider must bypass the fallback comparison, not merely be
# given a larger tolerance. Keep the direct return before IsWithinTolerance.
awk '
  /if \(directHeadCollider\)/ { direct = NR }
  /if \(!HeadshotRules\.IsWithinTolerance/ { fallback = NR }
  END { exit !(direct && fallback && direct < fallback) }
' "$logic"

# Qualification is one prefab-agnostic rule derived from live geometry.
if rg -ni 'lox|GetPrefabName|PrefabName|allowlist' "$logic" "$rules"; then
  printf 'headshot qualification must not special-case mobs or prefabs\n' >&2
  exit 1
fi

# The stateless slice must not introduce per-shot result/adrenaline protocols.
if rg -n 'AddAdrenaline|SNIPED|nonce|timeout|RPC_|Character\.Damage' "$logic" "$patch" "$rules"; then
  printf 'headshots must not add adrenaline, result protocol, or a Character.Damage hook\n' >&2
  exit 1
fi

printf 'headshot source invariants passed\n'
