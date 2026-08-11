#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feedback="$root/src/CombatFeedback/CombatFeedback.cs"
patches="$root/src/CombatFeedback/CombatFeedbackPatches.cs"
tuning="$root/src/CombatFeedback/CombatFeedbackTuning.cs"
headshots="$root/src/Archery/HeadshotLogic.cs"
mining="$root/src/Mining/MiningProgression.cs"
woodcutting="$root/src/Woodcutting/WoodcuttingProgression.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_camera="$source_tree/GameCamera.cs"

rg -Fq '[HarmonyPatch(typeof(GameCamera), "LateUpdate")]' "$patches"
rg -Fq 'player.GetAttackDrawPercentage()' "$feedback"
rg -Fq 'camera.m_fov - currentFocusReduction' "$feedback"
rg -Fq 'camera.m_skyCamera.fieldOfView = fieldOfView' "$feedback"
rg -Fq 'Time.unscaledDeltaTime' "$feedback"
rg -Fq 'player.IsDead()' "$feedback"
rg -Fq 'player.IsTeleporting()' "$feedback"
rg -Fq 'player.InCutscene()' "$feedback"
rg -Fq 'player.IsAttached()' "$feedback"
rg -Fq 'GameCamera.InFreeFly()' "$feedback"
rg -Fq 'camera.AddShake(' "$feedback"

test "$(rg -c 'RequestShake\(CombatFeedbackTrigger.Headshot\)' "$headshots")" -eq 1
test "$(rg -c 'RequestShake\(CombatFeedbackTrigger.Cleave\)' "$woodcutting")" -eq 1
test "$(rg -c 'RequestShake\(CombatFeedbackTrigger.MiningAoe\)' "$mining")" -eq 1
request_call_count="$(rg -n --glob '*.cs' 'CombatFeedbackController\.RequestShake' "$root/src" | wc -l | tr -d ' ')"
test "$request_call_count" -eq 3

# Valheim owns the final camera-shake preference and composition. Its current
# implementation is one strongest-wins intensity, not an additive stack.
rg -Fq 'if (!(num3 < m_shakeIntensity))' "$native_camera"
rg -Fq 'm_shakeIntensity = num3;' "$native_camera"
if rg -Fq 'm_shakeIntensity +=' "$native_camera"; then
  printf 'native camera shake composition changed from strongest-wins\n' >&2
  exit 1
fi

if rg -n 'm_fov\s*=|m_distance|FreezeFrame|RPC_|MusicMan|EnvMan|EffectList|Instantiate' "$root/src/CombatFeedback"; then
  printf 'combat feedback must stay local, transient, and camera-only\n' >&2
  exit 1
fi

rg -Fq 'BowFocusMaxReductionDegrees' "$tuning"
rg -Fq 'BowFocusNarrowSmoothSeconds' "$tuning"
rg -Fq 'BowFocusRestoreSmoothSeconds' "$tuning"
rg -Fq 'HeadshotShakeStrength' "$tuning"
rg -Fq 'CleaveShakeStrength' "$tuning"
rg -Fq 'MiningAoeShakeStrength' "$tuning"
rg -Fq 'ShakeStrengthCap' "$tuning"
rg -Fq 'ShakeCoalesceSeconds' "$tuning"

dotnet run --project "$root/tests/combat-feedback/CombatFeedbackTests.csproj"

printf 'combat feedback source and behavior checks passed\n'
