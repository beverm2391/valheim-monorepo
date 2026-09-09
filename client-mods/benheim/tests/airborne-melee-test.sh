#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(cd "$root/../.." && pwd)"
logic="$root/src/WeaponRhythm/AirborneMelee.cs"
patches="$root/src/WeaponRhythm/AirborneMeleePatches.cs"
outcome="$root/src/WeaponRhythm/PerfectImpactOutcome.cs"
diagnostics="$root/src/WeaponRhythm/PerfectImpactDiagnostics.cs"
delivery="$root/src/WeaponRhythm/PerfectImpactOutcomeDelivery.cs"
tuning="$root/src/WeaponRhythm/AirborneMeleeTuning.cs"
source_hash="210616393e3b997cee6293380ba467d62af326f14d2f4a3c1d37b46094bbaf6b"
decompiler_id="ilspy-ef475d235f0fcb13c7010d4b7c587a14d055f824b8349b9b5dfdeccb2e1b2406"
native_tree="$repo_root/backups/migration-1.0-porting/decompiled-source/$source_hash/projects/$decompiler_id"
native_assembly="$repo_root/backups/migration-1.0-porting/1.0/macos/Managed/assembly_valheim.dll"
ilspy_path="${ILSPY_PATH:-$HOME/.dotnet/tools/ilspycmd}"
native_attack="$native_tree/Attack.cs"
native_character="$native_tree/Character.cs"
native_humanoid="$native_tree/Humanoid.cs"

# The area-hit method is compiler-shaped, so source text alone cannot prove
# Harmony resolves the exact generated method. Use only the preserved 1.0
# evidence, never an installed game assembly.
if [[ ! -x "$ilspy_path" || ! -f "$native_assembly" || ! -f "$native_attack" ]]; then
  printf 'airborne melee: preserved 1.0 IL evidence is unavailable\n' >&2
  exit 1
fi
attack_il="$(mktemp "${TMPDIR:-/tmp}/benheim-airborne-attack.XXXXXX")"
trap 'rm -f "$attack_il"' EXIT
"$ilspy_path" --disable-updatecheck -il -t Attack \
  "$native_assembly" > "$attack_il"

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
  "instance void '<DoAreaAttack>g__checkHits|27_0' (" \
  "} // end of method Attack::'<DoAreaAttack>g__checkHits|27_0'"

rg -Fq 'yield return RequireAttackMethod("DoMeleeAttack")' "$patches"
rg -Fq 'yield return RequireAreaHitMethod()' "$patches"
rg -Fq 'AreaHitMethodPrefix = "<DoAreaAttack>g__checkHits|"' "$patches"
rg -Fq 'AccessTools.GetDeclaredMethods(typeof(Attack))' "$patches"
rg -Fq 'StartsWith(AreaHitMethodPrefix, StringComparison.Ordinal)' "$patches"
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
rg -Fq 'WorldFeedback.ShowAbove(' "$logic"
rg -Fq 'target.transform,' "$logic"
rg -Fq 'contactPoint - target.transform.position,' "$logic"
rg -Fq 'CombatFeedbackController.RequestShake(CombatFeedbackTrigger.PerfectImpact)' "$logic"
rg -Fq 'target.Damage(hit)' "$logic"
! rg -Fq 'TopLeftFeedbackHud' "$logic"
! rg -Fq 'TopLeftFeedbackResult' "$logic"
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
rg -Fq '.Boolean("feedback_requested", outcome.FeedbackRequested)' "$diagnostics"
rg -Fq '.String("feedback_seam", outcome.FeedbackSeam)' "$diagnostics"
! rg -Fq '.String("feedback",' "$diagnostics"
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
