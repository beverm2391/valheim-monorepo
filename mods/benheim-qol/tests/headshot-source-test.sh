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

# Damage is modified once and native stagger remains at its baseline.
grep -Fq 'hit.m_damage.Modify(multiplier)' "$logic"
grep -Fq 'CompensatedStaggerMultiplier' "$logic"
grep -Fq 'target.m_critHitEffects.Create' "$logic"
grep -Fq 'if (!target.IsStaggering())' "$logic"
grep -Fq 'WorldFeedback.ShowAbove' "$logic"
grep -Fq 'Diagnostics.Event("Headshots"' "$logic"
grep -Fq 'HeadTolerance' "$rules"

# The stateless slice must not introduce per-shot result/adrenaline protocols.
if rg -n 'AddAdrenaline|SNIPED|nonce|timeout|RPC_|Character\.Damage' "$logic" "$patch" "$rules"; then
  printf 'headshots must not add adrenaline, result protocol, or a Character.Damage hook\n' >&2
  exit 1
fi

printf 'headshot source invariants passed\n'
