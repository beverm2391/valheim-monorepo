#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feedback="$root/src/CombatFeedback/CombatFeedback.cs"
settings="$root/src/CombatFeedback/BenheimFxSettings.cs"
patches="$root/src/CombatFeedback/CombatFeedbackPatches.cs"
tuning="$root/src/CombatFeedback/CombatFeedbackTuning.cs"
config_ui="$root/src/Shortcuts/ShortcutOverlayConfig.cs"
native_templates="$root/src/Shortcuts/NativeTemplates.cs"
tabs="$root/src/Shortcuts/ShortcutOverlayTabs.cs"
headshots="$root/src/Archery/HeadshotLogic.cs"
mining="$root/src/Mining/MiningProgression.cs"
woodcutting="$root/src/Woodcutting/WoodcuttingProgression.cs"
airborne_melee="$root/src/WeaponRhythm/AirborneMelee.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_camera="$source_tree/GameCamera.cs"
native_accessibility="$source_tree/Valheim/SettingsGui/AccessibilitySettings.cs"

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
rg -Fq 'BenheimFxSettings.BowFocusEnabled' "$feedback"
rg -Fq 'RestoreFocusSmoothly(camera, resolvedMainCamera, "benheim_fx_disabled")' "$feedback"
rg -Fq 'BenheimFxSettings.CombatShakeEnabled' "$feedback"
rg -Fq 'LogShakeSuppressed(trigger, "benheim_fx_disabled")' "$feedback"

rg -Fq 'config.Bind(' "$settings"
rg -Fq 'internal static bool DangerArrivalEnabled' "$settings"
test "$(rg -c 'config.Bind\(' "$settings")" -eq 4
rg -Fq 'Object.Instantiate(templates.Checkbox' "$config_ui"
rg -Fq 'toggle.SetIsOnWithoutNotify(value)' "$config_ui"
test "$(rg -c '= AddFxToggle\(' "$config_ui")" -eq 4
rg -Fq 'HorizontalLayoutGroup rowLayout' "$config_ui"
rg -Fq 'TMP_Text toggleLabel = CreateText(' "$config_ui"
rg -Fq '"FxToggleLabel"' "$config_ui"
rg -Fq 'toggleLabel.text = label;' "$config_ui"
rg -Fq '"fx_config_built"' "$config_ui"
rg -Fq '"toggles=4 labels=4 label_layout=explicit_native_text"' "$config_ui"
if rg -Fq 'GetComponentInChildren<TMP_Text>' "$config_ui"; then
  printf 'config labels must be explicit, not inferred from the checkbox prefab hierarchy\n' >&2
  exit 1
fi
rg -Fq 'bowFocusToggle.interactable = masterEnabled' "$config_ui"
rg -Fq 'combatShakeToggle.interactable = masterEnabled' "$config_ui"
rg -Fq 'dangerArrivalToggle.interactable = masterEnabled' "$config_ui"
rg -Fq 'AccessTools.Field(typeof(AccessibilitySettings), "m_cameraShake")' "$native_templates"
rg -Fq 'private Toggle m_cameraShake;' "$native_accessibility"
rg -Fq 'AddTab(buttons, templates, ShortcutTab.Config, "Benheim Config", ConfigAccent);' "$tabs"

test "$(rg -c 'RequestShake\(CombatFeedbackTrigger.Headshot\)' "$headshots")" -eq 1
test "$(rg -c 'RequestShake\(CombatFeedbackTrigger.Cleave\)' "$woodcutting")" -eq 1
test "$(rg -c 'RequestShake\(CombatFeedbackTrigger.MiningAoe\)' "$mining")" -eq 1
test "$(rg -c 'RequestShake\(CombatFeedbackTrigger.PerfectImpact\)' "$airborne_melee")" -eq 1
request_call_count="$(rg -n --glob '*.cs' 'CombatFeedbackController\.RequestShake' "$root/src" | wc -l | tr -d ' ')"
test "$request_call_count" -eq 4

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
rg -Fq 'NativeAxeHitShakeStrength' "$tuning"
rg -Fq 'MiningAoeShakeStrength' "$tuning"
rg -Fq 'PerfectImpactShakeStrength' "$tuning"
rg -Fq 'ShakeStrengthCap' "$tuning"
rg -Fq 'ShakeCoalesceSeconds' "$tuning"

dotnet run --project "$root/tests/combat-feedback/CombatFeedbackTests.csproj"

printf 'combat feedback source and behavior checks passed\n'
