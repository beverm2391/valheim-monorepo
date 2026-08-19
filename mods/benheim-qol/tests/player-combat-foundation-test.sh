#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patches="$root/src/Adrenaline/AdrenalinePatches.cs"
observation="$root/src/PlayerCombat/PerfectDefenseObservation.cs"
outcome_identity="$root/src/PlayerCombat/PerfectDefenseOutcomeDeduplicator.cs"
runtime="$root/src/PlayerCombat/PlayerCombatRuntime.cs"
diagnostics="$root/src/PlayerCombat/PlayerCombatDiagnostics.cs"
native_patches="$root/src/PlayerCombat/PlayerCombatPatches.cs"
controller="$root/src/PlayerCombat/PlayerCombatController.cs"
clutch="$root/src/PlayerCombat/ClutchMechanic.cs"
untouchable="$root/src/PlayerCombat/UntouchableMechanic.cs"
berserker="$root/src/PlayerCombat/BerserkerMechanic.cs"
effects="$root/src/PlayerCombat/EarnedStateEffects.cs"
presentation="$root/src/PlayerCombat/EarnedStatePresentation.cs"
adrenaline_feedback="$root/src/Adrenaline/AdrenalineFeedback.cs"
plugin="$root/src/Plugin.cs"

# The outer hooks only open candidates. Valheim's nested adrenaline callback
# confirms one immutable fact before positive-value filtering changes v.
grep -Fq 'PerfectDefenseObservation.BeginParry(__instance, hit, attacker);' "$patches"
grep -Fq 'PerfectDefenseObservation.BeginDodge(__instance);' "$patches"
grep -Fq 'PerfectDefenseConfirmation confirmation =' "$patches"
grep -Fq 'PerfectDefenseObservation.ConfirmFromNativeAdrenaline(__instance);' "$patches"
grep -Fq 'confirmation == PerfectDefenseConfirmation.DuplicateNativeOutcome' "$patches"
grep -Fq 'v = 0f;' "$patches"
prefix_body="$(sed -n '/private static void Prefix(Player __instance, ref float v/,/private static void Postfix/p' "$patches")"
if [[ "$prefix_body" != *'if (v > 0f)'* ]]; then
  printf 'perfect-defense confirmation and positive grant handling must share Player.AddAdrenaline Prefix\n' >&2
  exit 1
fi
confirm_line="$(grep -n 'ConfirmFromNativeAdrenaline(__instance)' "$patches" | cut -d: -f1)"
positive_line="$(grep -n 'if (v > 0f)' "$patches" | cut -d: -f1)"
if (( confirm_line >= positive_line )); then
  printf 'perfect defense must confirm before positive grant filtering\n' >&2
  exit 1
fi
grep -Fq 'current.Confirmed = true;' "$observation"
grep -Fq 'ConfirmedOutcomes.TryAccept(current.OutcomeIdentity' "$observation"
grep -Fq 'ConditionalWeakTable<object, TokenHolder>' "$outcome_identity"
grep -Fq 'accepted.TryGetValue(identity' "$outcome_identity"
grep -Fq 'NativeAttackOutcomeIdentities<Attack>' "$observation"
grep -Fq '[HarmonyPatch(typeof(Attack), nameof(Attack.OnAttackTrigger))]' "$observation"
grep -Fq 'if (hit.m_ranged)' "$observation"
grep -Fq 'attack.m_loopingAttack' "$observation"
grep -Fq 'new OutcomeIdentity(hit, "ranged_hit")' "$observation"
grep -Fq '"duplicate_native_outcome"' "$observation"
grep -Fq 'new PerfectDefenseConfirmed(' "$observation"

# Gameplay subscribers are ordered before whole-event diagnostics. Remote
# diagnostics remain behind the existing DiagnosticEvent route.
controller_line="$(grep -n 'Subscribe<PerfectDefenseConfirmed>(ObservePerfectDefense)' "$runtime" | cut -d: -f1)"
diagnostic_line="$(grep -n 'Subscribe<PerfectDefenseConfirmed>(PlayerCombatDiagnostics.Project)' "$runtime" | cut -d: -f1)"
if (( controller_line >= diagnostic_line )); then
  printf 'gameplay controller must run before diagnostic projection\n' >&2
  exit 1
fi
grep -Fq 'Diagnostics.Emit(diagnosticEvent);' "$diagnostics"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "perfect_defense_confirmed")' "$diagnostics"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "game_event_subscriber_failed")' "$runtime"
if grep -Fq 'Diagnostics.Event(' "$runtime"; then
  printf 'Player Combat diagnostics must use the whole typed-event route\n' >&2
  exit 1
fi

# CLUTCH uses the immutable health snapshot and one native six-second SE_Stats.
# Its one-second native ticks continue across ResetTime refreshes, so each
# refreshed six-second window retains the approved ten-health-per-second rate.
grep -Fq 'perfectDefense.Context.Health < HealthThreshold' "$clutch"
grep -Fq 'internal const float HealthThreshold = 30f;' "$clutch"
grep -Fq 'effect.m_ttl = DurationSeconds;' "$clutch"
grep -Fq 'effect.m_tickInterval = 1f;' "$clutch"
grep -Fq 'effect.m_healthPerTick = HealthPerSecond;' "$clutch"
grep -Fq 'HealthIconItemPrefab = "MeadHealthLingering"' "$clutch"
grep -Fq 'HealthIconStatusEffect = "Potion_health_lingering"' "$clutch"
grep -Fq 'EarnedStateOutputResult.Refreshed()' "$effects"

# UNTOUCHABLE is one mixed streak with three indefinite, replacing tiers.
grep -Fq 'if (streak >= 12)' "$untouchable"
grep -Fq 'if (streak >= 8)' "$untouchable"
grep -Fq 'return streak >= 5 ? 1 : 0;' "$untouchable"
grep -Fq '1 => 1.10f' "$untouchable"
grep -Fq '2 => 1.20f' "$untouchable"
grep -Fq '3 => 1.30f' "$untouchable"
grep -Fq 'effect.m_ttl = 0f;' "$untouchable"
grep -Fq 'effect.m_modifyAttackSkill = Skills.SkillType.All;' "$untouchable"
grep -Fq 'DamageIconItemPrefab = "TrinketSilverDamage"' "$untouchable"
grep -Fq 'Deactivate(' "$controller"
grep -Fq 'EarnedStateTransitionReason.AcceptedDamage' "$controller"

# BERSERKER consumes the server-authoritative typed transition without owning
# kill counts or chain timing. Its two native tiers are mutually exclusive.
grep -Fq 'Publish(BerserkerChainTransition transition)' "$runtime"
grep -Fq 'Observe(BerserkerChainTransition transition)' "$controller"
grep -Fq 'ZNet.instance?.GetTimeSeconds() ?? transition.ServerTimeSeconds' "$controller"
grep -Fq 'effect.m_ttl = DurationSeconds;' "$berserker"
grep -Fq 'effect.m_staminaRegenMultiplier = tier == 1 ? 1.5f : 2f;' "$berserker"
grep -Fq 'HitData.DamageType.Blunt' "$berserker"
grep -Fq 'HitData.DamageType.Slash' "$berserker"
grep -Fq 'HitData.DamageType.Pierce' "$berserker"
if grep -Fq 'HitData.DamageType.Physical' "$berserker"; then
  printf 'native DamageModifiers.Apply ignores aggregate Physical pairs\n' >&2
  exit 1
fi
grep -Fq 'ResistanceIconItemPrefab = "TrinketSilverResist"' "$berserker"
grep -Fq '"BERSERKER!"' "$berserker"
grep -Fq '"SLAUGHTERHOUSE!"' "$berserker"

# One originating defense owns one local Bonus text instance and at most one
# native charm one-shot. Discovery banners and network ShowText are not earned-
# state presentation seams.
grep -Fq 'string.Join("\n", lines)' "$presentation"
grep -Fq 'WorldFeedback.ShowAbovePlayer(player' "$presentation"
grep -Fq 'pendingCharmTransition' "$presentation"
grep -Fq '!nativeCharmActivated' "$presentation"
grep -Fq 'player.GetAdrenaline() < award.Maximum' "$adrenaline_feedback"
grep -Fq 'm_adrenalinePopEffects' "$presentation"
grep -Fq 'activationEffects.Create(' "$presentation"
if grep -Fq 'ShowBiomeFoundMsg' "$presentation"; then
  printf 'earned-state presentation must not use the discovery banner\n' >&2
  exit 1
fi
if grep -Fq 'DamageText.instance?.ShowText' "$adrenaline_feedback"; then
  printf 'perfect-defense feedback must use the shared local WorldFeedback dispatch\n' >&2
  exit 1
fi

# New decisions and output lifecycle project through whole typed events.
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "clutch_decision")' "$diagnostics"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "untouchable_streak_changed")' "$diagnostics"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "untouchable_reset")' "$diagnostics"
grep -Fq '"earned_state_activated"' "$diagnostics"
grep -Fq '"earned_state_refreshed"' "$diagnostics"
grep -Fq '"earned_state_expired"' "$diagnostics"
grep -Fq '"earned_state_activation_rejected"' "$diagnostics"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "berserker_chain_transition")' "$diagnostics"

# Stable lifecycle and native seams are explicit; no frame update publishes
# combat traffic.
grep -Fq '[HarmonyPatch(typeof(ObjectDB), "Awake")]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.UseHealth))]' "$native_patches"
if grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.SetHealth))]' "$native_patches"; then
  printf 'food-driven maximum-health normalization must stay outside the accepted health-loss observer\n' >&2
  exit 1
fi
grep -Fq '[HarmonyPatch(typeof(Player), "OnDeath")]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(Player), "OnDestroy")]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnDestroy")]' "$native_patches"
grep -Fq 'PlayerCombatRuntime.BeginSession();' "$plugin"
grep -Fq 'PlayerCombatRuntime.EndSession();' "$plugin"
if rg -n 'PlayerCombatRuntime\.Publish|PerfectDefenseObservation' "$plugin" | grep -Fq 'Update'; then
  printf 'Player Combat must not publish per-frame events\n' >&2
  exit 1
fi

# Native harm enters through ApplyDamage or UseHealth. Food decay instead calls
# SetMaxHealth, whose SetHealth clamp must not reset UNTOUCHABLE.
native_tree="$("$root/scripts/ensure-valheim-source.sh" | tail -n 1)"
grep -Fq 'ApplyDamage(hit, showDamageText: true, triggerEffects: true' "$native_tree/Character.cs"
grep -Fq 'm_character.ApplyDamage(hitData, showDamageText: true, triggerEffects: false);' "$native_tree/SE_Burning.cs"
grep -Fq 'm_character.ApplyDamage(hitData, showDamageText: true, triggerEffects: false);' "$native_tree/SE_Poison.cs"
grep -Fq 'm_character.ApplyDamage(hitData, showDamageText: true, triggerEffects: false);' "$native_tree/SE_Smoke.cs"
grep -Fq 'public void UseHealth(float hp)' "$native_tree/Character.cs"
grep -Fq 'SetHealth(health);' "$native_tree/Character.cs"
grep -Fq 'SetMaxHealth(hp, flashBar: true);' "$native_tree/Player.cs"
grep -Fq 'if (GetHealth() > health)' "$native_tree/Character.cs"
grep -Fq 'Attack attack = ((!secondaryAttack)' "$native_tree/Humanoid.cs"
grep -Fq 'm_currentAttack = attack;' "$native_tree/Humanoid.cs"
grep -Fq 'public void OnAttackTrigger()' "$native_tree/Attack.cs"
grep -Fq 'hitData.m_ranged = true;' "$native_tree/Projectile.cs"
grep -Fq 'hitData.m_ranged = true;' "$native_tree/Aoe.cs"

# Native SE_Stats ticks call owner-routed Heal; Heal caps at maximum health.
grep -Fq 'if (m_healthPerTick > 0f)' "$native_tree/SE_Stats.cs"
grep -Fq 'm_character.Heal(m_healthPerTick);' "$native_tree/SE_Stats.cs"
grep -Fq 'float num = Mathf.Min(health + hp, GetMaxHealth());' "$native_tree/Character.cs"

# Native all-skill outgoing damage and indefinite status semantics are direct.
grep -Fq 'm_modifyAttackSkill == Skills.SkillType.All' "$native_tree/SE_Stats.cs"
grep -Fq 'hitData.m_damage.Modify(m_damageModifier);' "$native_tree/SE_Stats.cs"
grep -Fq 'if (m_ttl > 0f && m_time > m_ttl)' "$native_tree/StatusEffect.cs"
grep -Fq 'case DamageModifier.SlightlyResistant:' "$native_tree/HitData.cs"
grep -Fq 'num *= 0.75f;' "$native_tree/HitData.cs"
grep -Fq 'case DamageModifier.Resistant:' "$native_tree/HitData.cs"
grep -Fq 'num *= 0.5f;' "$native_tree/HitData.cs"
grep -Fq 'staminaRegen += m_staminaRegenMultiplier - 1f;' "$native_tree/SE_Stats.cs"

# The exact local charm one-shot is a presentation-only EffectList call.
grep -Fq 'm_adrenalinePopEffects.Create(base.transform.position, Quaternion.identity);' "$native_tree/Player.cs"
grep -Fq 'public bool HasEffects()' "$native_tree/EffectList.cs"

dotnet run --project "$root/tests/player-combat-foundation/PlayerCombatFoundationTests.csproj" -c Release
