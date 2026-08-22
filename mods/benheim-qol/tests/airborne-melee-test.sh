#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
logic="$root/src/WeaponRhythm/AirborneMelee.cs"
patches="$root/src/WeaponRhythm/AirborneMeleePatches.cs"
outcome="$root/src/WeaponRhythm/PerfectImpactOutcome.cs"
diagnostics="$root/src/WeaponRhythm/PerfectImpactDiagnostics.cs"
delivery="$root/src/WeaponRhythm/PerfectImpactOutcomeDelivery.cs"
tuning="$root/src/WeaponRhythm/AirborneMeleeTuning.cs"
native_tree="$($root/scripts/ensure-valheim-source.sh)"
native_attack="$native_tree/Attack.cs"
native_character="$native_tree/Character.cs"
native_humanoid="$native_tree/Humanoid.cs"

# Resolve the installed assembly through the evidence owner used by the source
# cache. The area-hit method is compiler-shaped, so source text cannot prove
# that Harmony still resolves and patches the exact generated method.
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
rg -Fq 'if (replaced != 1)' "$patches"
rg -Fq 'new CodeInstruction(OpCodes.Ldarg_0)' "$patches"
rg -Fq 'typeof(IDestructible), typeof(HitData), typeof(Attack)' "$patches"
rg -Fq 'nameof(AirborneMelee.DamageMeleeTarget)' "$patches"
rg -Fq '[HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]' "$patches"
rg -Fq 'if (__result && ___m_currentAttack != null)' "$patches"
rg -Fq 'AirborneMelee.ObserveAttackStarted(' "$patches"
! rg -Fq 'RequireInputField' "$patches"
! rg -Fq 'FieldInfo' "$patches"
! rg -Fq 'Attack.Update' "$patches"
! rg -Fq 'Attack.Stop' "$patches"

rg -Fq 'ConditionalWeakTable<Attack, AirborneMeleeSwingState>' "$logic"
rg -Fq 'internal static void ObserveAttackStarted(' "$logic"
rg -Fq 'character != localPlayer' "$logic"
rg -Fq 'secondaryAttack ? "secondary" : "primary"' "$logic"
rg -Fq 'Character? targetCharacter = target as Character' "$logic"
rg -Fq 'attacker == localPlayer' "$logic"
rg -Fq 'Vector3 towardContact = hit.m_point - localPlayer.transform.position' "$logic"
rg -Fq 'AirborneMeleeRules.ProjectPlanarVelocityToward(' "$logic"
rg -Fq 'AirborneMeleeRules.ResolveContact(' "$logic"
rg -Fq 'bool firstResolution = state.TryResolve(resolution)' "$logic"
rg -Fq 'state.Qualified && resolution == PerfectImpactResolution.Applied' "$logic"
rg -Fq 'hit.m_damage.Modify(AirborneMeleeTuning.DamageMultiplier)' "$logic"
rg -Fq 'hit.m_staggerMultiplier *= AirborneMeleeTuning.StaggerMultiplier' "$logic"
rg -Fq 'PerfectImpactDiagnostics.Emit(' "$logic"
rg -Fq 'PerfectImpactOutcomeDelivery.Deliver(' "$logic"
rg -Fq '() => target.Damage(hit)' "$logic"
rg -Fq 'ReportOptionalOutcomeFailure' "$logic"
rg -Fq 'TopLeftFeedbackHud.ShowTransient(SuccessMessage)' "$logic"
rg -Fq 'CombatFeedbackController.RequestShake(CombatFeedbackTrigger.PerfectImpact)' "$logic"
rg -Fq 'target.Damage(hit)' "$logic"
! rg -Fq 'BeginAttackAttempt' "$logic"
! rg -Fq 'CompleteAttackAttempt' "$logic"
! rg -Fq 'start_forward_speed' "$logic"
! rg -Fq 'start_grounded' "$logic"

rg -Fq 'internal sealed class PerfectImpactOutcome' "$outcome"
rg -Fq 'internal bool Qualified => Resolution == PerfectImpactResolution.Applied' "$outcome"
rg -Fq 'internal bool TryResolve(PerfectImpactResolution resolution)' "$outcome"
rg -Fq 'DiagnosticEvent.Create("WeaponRhythm", "perfect_impact_outcome")' "$diagnostics"
rg -Fq '.Boolean("qualified", outcome.Qualified)' "$diagnostics"
rg -Fq '.Number("toward_target_speed", outcome.TowardTargetSpeed)' "$diagnostics"
rg -Fq '.Number("approach_threshold", outcome.ApproachThreshold)' "$diagnostics"
rg -Fq '.String("feedback", outcome.Feedback)' "$diagnostics"
rg -Fq 'RunOptional(present, reportFailure)' "$delivery"
rg -Fq 'RunOptional(emitDiagnostic, reportFailure)' "$delivery"
rg -Fq 'nativeDamage();' "$delivery"

rg -Fq 'internal const float DescentThreshold = -0.5f' "$tuning"
rg -Fq 'internal const float ApproachSpeedThreshold = 5.5f' "$tuning"
rg -Fq 'internal const float DamageMultiplier = 1.15f' "$tuning"
rg -Fq 'internal const float StaggerMultiplier = 3f' "$tuning"
rg -Fq 'public override bool StartAttack(Character target, bool secondaryAttack)' "$native_humanoid"
rg -Fq 'm_attack.Clone()' "$native_humanoid"
rg -Fq 'm_secondaryAttack.Clone()' "$native_humanoid"
rg -Fq 'm_currentAttack = attack;' "$native_humanoid"
rg -Fq 'private void DoMeleeAttack()' "$native_attack"
rg -Fq 'private void DoAreaAttack()' "$native_attack"
rg -Fq 'case AttackType.Horizontal:' "$native_attack"
rg -Fq 'case AttackType.Vertical:' "$native_attack"
rg -Fq 'case AttackType.Area:' "$native_attack"
rg -Fq 'public Vector3 GetVelocity()' "$native_character"
rg -Fq 'return m_body.linearVelocity;' "$native_character"

if rg -n --glob '*.cs' \
    'HarmonyPatch\(typeof\(Character\).*Damage|RPC_|FixedUpdate\(' \
    "$root/src/WeaponRhythm"; then
  printf 'Perfect Impact must stay on the outgoing native melee hit seam\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/airborne-melee/AirborneMeleeTests.csproj"

printf 'Perfect Impact source and behavior checks passed\n'
