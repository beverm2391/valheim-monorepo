#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patches="$root/src/Adrenaline/AdrenalinePatches.cs"
observation="$root/src/PlayerCombat/PerfectDefenseObservation.cs"
runtime="$root/src/PlayerCombat/PlayerCombatRuntime.cs"
diagnostics="$root/src/PlayerCombat/PlayerCombatDiagnostics.cs"
native_patches="$root/src/PlayerCombat/PlayerCombatPatches.cs"
plugin="$root/src/Plugin.cs"

# The outer hooks only open candidates. Valheim's nested adrenaline callback
# confirms one immutable fact before positive-value filtering changes v.
grep -Fq 'PerfectDefenseObservation.BeginParry(__instance, attacker);' "$patches"
grep -Fq 'PerfectDefenseObservation.BeginDodge(__instance);' "$patches"
prefix_body="$(sed -n '/private static void Prefix(Player __instance, ref float v/,/__state =/p' "$patches")"
if [[ "$prefix_body" != *'PerfectDefenseObservation.ConfirmFromNativeAdrenaline(__instance);'* \
   || "$prefix_body" != *'if (v > 0f)'* ]]; then
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

# Stable lifecycle and native seams are explicit; no frame update publishes
# combat traffic.
grep -Fq '[HarmonyPatch(typeof(ObjectDB), "Awake")]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(Player), "OnDeath")]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(Player), "OnDestroy")]' "$native_patches"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnDestroy")]' "$native_patches"
grep -Fq 'PlayerCombatRuntime.BeginSession();' "$plugin"
grep -Fq 'PlayerCombatRuntime.EndSession();' "$plugin"
if rg -n 'PlayerCombatRuntime\.Publish|PerfectDefenseObservation' "$plugin" | grep -Fq 'Update'; then
  printf 'Player Combat must not publish per-frame events\n' >&2
  exit 1
fi

# Native direct and damage-over-time paths converge on ApplyDamage after armor,
# block, and resistance resolution. Observing RPC_Damage would miss these ticks.
native_tree="$("$root/scripts/ensure-valheim-source.sh" | tail -n 1)"
grep -Fq 'ApplyDamage(hit, showDamageText: true, triggerEffects: true' "$native_tree/Character.cs"
grep -Fq 'm_character.ApplyDamage(hitData, showDamageText: true, triggerEffects: false);' "$native_tree/SE_Burning.cs"
grep -Fq 'm_character.ApplyDamage(hitData, showDamageText: true, triggerEffects: false);' "$native_tree/SE_Poison.cs"
grep -Fq 'm_character.ApplyDamage(hitData, showDamageText: true, triggerEffects: false);' "$native_tree/SE_Smoke.cs"

dotnet run --project "$root/tests/player-combat-foundation/PlayerCombatFoundationTests.csproj" -c Release
