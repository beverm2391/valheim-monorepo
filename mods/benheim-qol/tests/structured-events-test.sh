#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_root="$(mktemp -d)"
trap 'rm -rf "$test_root"' EXIT

dotnet run --project "$root/tests/structured-events/StructuredEventTests.csproj"
python3 "$root/tests/query-events-remote-test.py"

events="$test_root/events.ndjson"
cat > "$events" <<'JSON'
{"timestamp":"2026-08-13T00:00:00Z","session":"s1","benheim_version":"0.1.60","domain":"Production","event":"station_fill_requested","schema":1,"operation_id":"complete","operation_phase":"start","station":"windmill#1","item":"Barley","requested":4}
{"timestamp":"2026-08-13T00:00:01Z","session":"s1","benheim_version":"0.1.60","domain":"Production","event":"station_fill_finished","schema":1,"operation_id":"complete","operation_phase":"terminal","station":"windmill#1","item":"Barley","accepted":4}
{"timestamp":"2026-08-13T00:00:02Z","session":"s2","benheim_version":"0.1.60","domain":"Production","event":"station_fill_requested","schema":1,"operation_id":"open","operation_phase":"start","station":"smelter#2","item":"IronScrap","requested":10}
{"timestamp":"2026-08-13T00:00:03Z","session":"s2","benheim_version":"0.1.60","domain":"Cooking","event":"owner_decision","schema":1,"station":"piece_oven#3","item":"LoxPieUncooked","accepted":false}
JSON

query="$root/scripts/query-events.py"
test "$(python3 "$query" "$events" --domain Cooking --item LoxPieUncooked | wc -l | tr -d ' ')" = 1
test "$(python3 "$query" "$events" --session s1 --station 'windmill#1' | wc -l | tr -d ' ')" = 2
test "$(python3 "$query" "$events" --operation-id complete | wc -l | tr -d ' ')" = 2
incomplete="$(python3 "$query" "$events" --incomplete --domain Production)"
grep -Fq '"operation_id":"open"' <<< "$incomplete"
! grep -Fq '"operation_id":"complete"' <<< "$incomplete"

printf '{bad json}\n' > "$test_root/bad.ndjson"
if python3 "$query" "$test_root/bad.ndjson" > /dev/null 2> "$test_root/error"; then
  echo "malformed JSON must fail visibly" >&2
  exit 1
fi
grep -Fq 'invalid JSON' "$test_root/error"

diagnostics="$root/src/Infrastructure/Diagnostics.cs"
cooking="$root/src/Production/CookingDiagnostics.cs"
cooking_bonus="$root/src/Production/CookingBonusPatches.cs"
shortcuts="$root/src/Shortcuts/NativeConsoleShortcut.cs"
remote="$root/src/Production/RemoteSmelterBatch.cs"
station_fill_diagnostics="$root/src/Production/StationFillDiagnostics.cs"
earned_state_effects="$root/src/PlayerCombat/EarnedStateEffects.cs"
grep -Fq 'Plugin.Log.LogInfo($"[diag][{feature}] {action}{suffix}")' "$diagnostics"
grep -Fq 'DiagnosticEvent.Create("Cooking", "requester_attempt")' "$cooking"
grep -Fq 'DiagnosticEvent.Create("Cooking", "owner_decision")' "$cooking"
grep -Fq 'DiagnosticEvent.Create("Cooking", "output_cooked")' "$cooking"
grep -Fq 'DiagnosticEvent.Create("Cooking", "output_spawned")' "$cooking"
grep -Fq 'DiagnosticEvent.Create("Cooking", "native_bonus_roll")' "$cooking_bonus"
grep -Fq 'DiagnosticEvent.Create("Shortcuts", "native_console_shortcut")' "$shortcuts"
grep -Fq '.String("previous_item", before[slot].Item)' "$cooking"
! grep -Fq '.String("input_item", before[slot].Item)' "$cooking"
grep -Fq '.String("operation_phase", "start")' "$remote"
grep -Fq '.String("operation_phase", "terminal")' "$remote"
grep -Fq '"request_failed", ex.Message' "$remote"
grep -Fq 'diagnosticEvent.String("error", error)' "$remote"
grep -Fq '.String("operation_phase", "start")' "$station_fill_diagnostics"
grep -Fq '.String("operation_phase", "terminal")' "$station_fill_diagnostics"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "earned_state_healing_tick")' "$earned_state_effects"
grep -Fq '.Number("native_health_before", healthBefore)' "$earned_state_effects"
grep -Fq '.Number("configured_health_per_tick", m_healthPerTick)' "$earned_state_effects"
grep -Fq '.Number("resolved_health_after", healedPlayer.GetHealth())' "$earned_state_effects"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "earned_state_outgoing_damage_applied")' "$earned_state_effects"
grep -Fq '.Number("configured_damage_multiplier", m_damageModifier)' "$earned_state_effects"
grep -Fq 'AddDamageFields(diagnosticEvent, "native", nativeInput);' "$earned_state_effects"
grep -Fq 'AddDamageFields(diagnosticEvent, "resolved", hitData.m_damage);' "$earned_state_effects"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "earned_state_stamina_regen_applied")' "$earned_state_effects"
grep -Fq '.Number("native_regen_multiplier", nativeInput)' "$earned_state_effects"
grep -Fq '.Number("configured_regen_multiplier", m_staminaRegenMultiplier)' "$earned_state_effects"
grep -Fq '.Number("resolved_regen_multiplier", staminaRegen)' "$earned_state_effects"
grep -Fq 'DiagnosticEvent.Create("PlayerCombat", "earned_state_physical_resistance_applied")' "$earned_state_effects"
grep -Fq '"native_blunt_modifier"' "$earned_state_effects"
grep -Fq '"configured_blunt_modifier"' "$earned_state_effects"
grep -Fq '"resolved_blunt_modifier"' "$earned_state_effects"

echo "structured diagnostic writer, domain wiring, and streaming query checks passed"
