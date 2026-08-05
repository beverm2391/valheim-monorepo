#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
input_state="$root/src/Infrastructure/InputState.cs"
farming_input="$root/src/Farming/FarmingInput.cs"
split_input="$root/src/Inventory/SplitStackPatches.cs"

actual_raw_input_files="$({
  rg -l '(Input|ZInput)\.(GetKey|GetKeyDown|GetButton|GetButtonDown)' \
    "$root/src" --glob '*.cs' || true
} | sed "s#^$root/##" | sort)"
expected_raw_input_files="$(printf '%s\n' \
  'src/Farming/FarmingInput.cs' \
  'src/Infrastructure/InputState.cs' \
  'src/Inventory/SplitStackPatches.cs' \
  'src/Shortcuts/ShortcutOverlay.cs')"

if [[ "$actual_raw_input_files" != "$expected_raw_input_files" ]]; then
  printf 'raw input calls must route through the text-entry policy\n' >&2
  diff -u <(printf '%s\n' "$expected_raw_input_files") \
    <(printf '%s\n' "$actual_raw_input_files") >&2 || true
  exit 1
fi

test "$(grep -Fc 'if (IsTextEntryActive())' "$input_state")" -eq 3
grep -Fq 'InputState.IsTextEntryActive()' "$farming_input"
grep -Fq 'The split dialog is the text-entry surface' "$split_input"
grep -Fq 'InputState.IsKeyDown(KeyCode.F7)' \
  "$root/src/Infrastructure/DiagnosticLogExporter.cs"
grep -Fq 'MenuShortcutDown()' \
  "$root/src/Shortcuts/ShortcutOverlay.cs"
grep -Fq 'RawKeyDown(KeyCode.B)' \
  "$root/src/Shortcuts/ShortcutOverlay.cs"

printf 'text-entry input routing checks passed\n'
