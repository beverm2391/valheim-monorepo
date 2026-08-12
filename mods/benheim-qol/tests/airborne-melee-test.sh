#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
logic="$root/src/WeaponRhythm/AirborneMelee.cs"
patches="$root/src/WeaponRhythm/AirborneMeleePatches.cs"
tuning="$root/src/WeaponRhythm/AirborneMeleeTuning.cs"
native_tree="$($root/scripts/ensure-valheim-source.sh)"
native_attack="$native_tree/Attack.cs"

# Resolve the exact installed assembly through the same evidence owner used by
# ensure-valheim-source. The generated area-hit method is compiler-shaped, so
# source-string checks alone cannot prove Harmony will resolve and patch it.
# shellcheck source=../scripts/valheim-source-lib.sh
source "$root/scripts/valheim-source-lib.sh"
valheim_source_resolve_assembly || {
  printf 'airborne melee: %s\n' "$VALHEIM_SOURCE_ERROR" >&2
  exit 1
}
valheim_source_resolve_ilspy || {
  printf 'airborne melee: %s\n' "$VALHEIM_SOURCE_ERROR" >&2
  exit 1
}
attack_il="$(mktemp "${TMPDIR:-/tmp}/benheim-airborne-attack.XXXXXX")"
trap 'rm -f "$attack_il"' EXIT
"$VALHEIM_SOURCE_ILSPY_PATH" --disable-updatecheck -il -t Attack \
  "$VALHEIM_SOURCE_ASSEMBLY_PATH" > "$attack_il"

assert_one_damage_call() {
  local signature="$1"
  local end_marker="$2"

  awk -v signature="$signature" -v end_marker="$end_marker" '
    index($0, signature) { active = 1; methods++ }
    active && index($0, "callvirt instance void IDestructible::Damage(class HitData)") {
      calls++
    }
    active && index($0, end_marker) { active = 0 }
    END {
      if (methods != 1 || calls != 1) {
        printf "expected one %s method with one direct damage call; methods=%d calls=%d\n", signature, methods, calls > "/dev/stderr"
        exit 1
      }
    }
  ' "$attack_il"
}

assert_one_damage_call \
  'instance void DoMeleeAttack () cil managed' \
  '} // end of method Attack::DoMeleeAttack'
assert_one_damage_call \
  "instance void '<DoAreaAttack>g__checkHits|26_0' (" \
  "} // end of method Attack::'<DoAreaAttack>g__checkHits|26_0'"

rg -Fq 'yield return RequireAttackMethod("DoMeleeAttack")' "$patches"
rg -Fq 'yield return RequireAttackMethod(AreaHitMethod)' "$patches"
rg -Fq '<DoAreaAttack>g__checkHits|26_0' "$patches"
rg -Fq 'if (replaced != 1)' "$patches"
rg -Fq 'code.opcode = OpCodes.Call' "$patches"
rg -Fq 'nameof(AirborneMelee.DamageMeleeTarget)' "$patches"

rg -Fq 'Character? targetCharacter = target as Character' "$logic"
rg -Fq 'attacker == localPlayer' "$logic"
rg -Fq '!localPlayer!.IsOnGround()' "$logic"
rg -Fq 'hit.m_damage.Modify(AirborneMeleeTuning.DamageMultiplier)' "$logic"
rg -Fq 'hit.m_staggerMultiplier *= AirborneMeleeTuning.StaggerMultiplier' "$logic"
rg -Fq 'target.Damage(hit)' "$logic"
rg -Fq 'airborne_melee_applied' "$logic"
rg -Fq 'airborne_melee_skipped' "$logic"

rg -Fq 'internal const float DamageMultiplier = 1.15f' "$tuning"
rg -Fq 'internal const float StaggerMultiplier = 2f' "$tuning"
rg -Fq 'private void DoMeleeAttack()' "$native_attack"
rg -Fq 'private void DoAreaAttack()' "$native_attack"

if rg -n --glob '*.cs' \
    'HarmonyPatch\(typeof\(Character\).*Damage|RPC_|Update\(|FixedUpdate\(' \
    "$root/src/WeaponRhythm"; then
  printf 'airborne melee must stay on the outgoing native melee hit seam\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/airborne-melee/AirborneMeleeTests.csproj"

printf 'airborne melee source and behavior checks passed\n'
