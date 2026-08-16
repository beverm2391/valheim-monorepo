#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
logic="$root/src/WeaponRhythm/AirborneMelee.cs"
patches="$root/src/WeaponRhythm/AirborneMeleePatches.cs"
tuning="$root/src/WeaponRhythm/AirborneMeleeTuning.cs"
native_tree="$($root/scripts/ensure-valheim-source.sh)"
native_attack="$native_tree/Attack.cs"
native_character="$native_tree/Character.cs"
native_humanoid="$native_tree/Humanoid.cs"
native_message_hud="$native_tree/MessageHud.cs"

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
rg -Fq 'new CodeInstruction(OpCodes.Ldarg_0)' "$patches"
rg -Fq 'code.MoveLabelsTo(loadAttack)' "$patches"
rg -Fq 'code.MoveBlocksTo(loadAttack)' "$patches"
rg -Fq 'typeof(IDestructible), typeof(HitData), typeof(Attack)' "$patches"
rg -Fq 'nameof(AirborneMelee.DamageMeleeTarget)' "$patches"
rg -Fq '[HarmonyPatch(typeof(Attack), nameof(Attack.Start))]' "$patches"
rg -Fq 'AirborneMelee.ObserveAttackStart(__instance, character, __result)' "$patches"
rg -Fq '[HarmonyPatch(typeof(Attack), nameof(Attack.Stop))]' "$patches"
rg -Fq 'AirborneMelee.ObserveAttackStop(__instance)' "$patches"

rg -Fq 'ConditionalWeakTable<Attack, AirborneMeleeSwingState>' "$logic"
rg -Fq 'internal static void ObserveAttackStart(Attack attack, Humanoid character, bool started)' "$logic"
rg -Fq 'character != localPlayer' "$logic"
rg -Fq 'localPlayer.IsOnGround()' "$logic"
rg -Fq 'Vector3 forward = localPlayer.transform.forward' "$logic"
rg -Fq 'AirborneMeleeRules.CanArm(' "$logic"
rg -Fq 'Diagnostics.NewOperationId()' "$logic"
rg -Fq 'airborne_melee_armed' "$logic"
rg -Fq 'airborne_melee_arm_rejected' "$logic"
test "$(rg -c '.Number\("damage_multiplier", AirborneMeleeTuning.DamageMultiplier\)' "$logic")" -eq 4
test "$(rg -c '.Number\("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier\)' "$logic")" -eq 4
test "$(rg -c '.String\("feedback", "not_requested"\)' "$logic")" -eq 3
rg -Fq 'Character? targetCharacter = target as Character' "$logic"
rg -Fq 'attacker == localPlayer' "$logic"
rg -Fq 'bool grounded = localPlayer!.IsOnGround()' "$logic"
rg -Fq 'Vector3 velocity = localPlayer.GetVelocity()' "$logic"
rg -Fq 'AirborneMeleeRules.CanConsume(' "$logic"
rg -Fq 'state.Resolve(qualifiesAtHit)' "$logic"
rg -Fq 'hit.m_damage.Modify(AirborneMeleeTuning.DamageMultiplier)' "$logic"
rg -Fq 'hit.m_staggerMultiplier *= AirborneMeleeTuning.StaggerMultiplier' "$logic"
rg -Fq 'TopLeftFeedbackResult feedbackResult = TopLeftFeedbackHud.ShowTransient(SuccessMessage)' "$logic"
rg -Fq 'CombatFeedbackController.RequestShake(CombatFeedbackTrigger.PerfectImpact)' "$logic"
rg -Fq 'target.Damage(hit)' "$logic"
rg -Fq 'airborne_melee_applied' "$logic"
rg -Fq 'airborne_melee_skipped' "$logic"
rg -Fq 'internal static void ObserveAttackStop(Attack attack)' "$logic"
rg -Fq '.String("reason", "no_character_contact")' "$logic"
rg -Fq 'SwingStates.Remove(attack)' "$logic"
rg -Fq '? "grounded_at_hit"' "$logic"
rg -Fq ': "rising_or_apex_at_hit"' "$logic"
rg -Fq '.String("operation_phase", "terminal")' "$logic"
rg -Fq '.String("operation_phase", armed ? "start" : "terminal")' "$logic"
rg -Fq '.Number("start_forward_speed", state.StartForwardSpeed)' "$logic"
rg -Fq '.Number("forward_speed_threshold", AirborneMeleeTuning.ForwardSpeedThreshold)' "$logic"
rg -Fq '.Number("vertical_speed", verticalSpeed)' "$logic"
rg -Fq '.Number("descent_threshold", AirborneMeleeTuning.DescentThreshold)' "$logic"
rg -Fq '.Number("damage_multiplier", AirborneMeleeTuning.DamageMultiplier)' "$logic"
rg -Fq '.Number("stagger_multiplier", AirborneMeleeTuning.StaggerMultiplier)' "$logic"
rg -Fq '.String("feedback", feedbackResult)' "$logic"
rg -Fq '.String("feedback", "not_requested")' "$logic"
rg -Fq 'TopLeftFeedbackResult.Placed => "placed"' "$logic"
rg -Fq 'TopLeftFeedbackResult.CreatedNotPlaced => "created_not_placed"' "$logic"
rg -Fq '_ => "unavailable"' "$logic"
! rg -Fq 'toward_target_speed' "$logic"
! rg -Fq 'lastFeedbackFrame' "$logic"

rg -Fq 'internal const float DescentThreshold = -0.5f' "$tuning"
rg -Fq 'internal const float ForwardSpeedThreshold = 7f' "$tuning"
rg -Fq 'internal const float DamageMultiplier = 1.15f' "$tuning"
rg -Fq 'internal const float StaggerMultiplier = 3f' "$tuning"
rg -Fq 'm_attack.Clone()' "$native_humanoid"
rg -Fq 'm_secondaryAttack.Clone()' "$native_humanoid"
rg -Fq 'if (attack.Start(this, m_body, m_zanim, m_animEvent, m_visEquipment, currentWeapon' "$native_humanoid"
rg -Fq 'm_currentAttack = attack;' "$native_humanoid"
rg -Fq 'm_currentAttack.Stop();' "$native_humanoid"
rg -Fq 'public void Stop()' "$native_attack"
rg -Fq 'm_attackDone = true;' "$native_attack"
rg -Fq 'private void DoMeleeAttack()' "$native_attack"
rg -Fq 'private void DoAreaAttack()' "$native_attack"
rg -Fq 'public Vector3 GetVelocity()' "$native_character"
rg -Fq 'return m_body.linearVelocity;' "$native_character"
rg -Fq 'm_messageText.CrossFadeAlpha(0f, 0f, ignoreTimeScale: true)' "$native_message_hud"
if rg -Fq 'feedback=shown' "$logic"; then
  printf 'Perfect Impact diagnostics must report the shared lane outcome, not claim shown\n' >&2
  exit 1
fi

if rg -n --glob '*.cs' \
    'HarmonyPatch\(typeof\(Character\).*Damage|RPC_|Update\(|FixedUpdate\(' \
    "$root/src/WeaponRhythm"; then
  printf 'airborne melee must stay on the outgoing native melee hit seam\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/airborne-melee/AirborneMeleeTests.csproj"

printf 'airborne melee source and behavior checks passed\n'
