#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
catalog="$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
warnings="$root/src/Shortcuts/ShortcutOverlayWarnings.cs"
tabs="$root/src/Shortcuts/ShortcutOverlayTabs.cs"

# Collision detection reads Valheim's effective current bindings, rather than
# hard-coding the game's defaults or adding a parallel binding registry.
grep -Fq 'AccessTools.Field(typeof(ZInput), "m_buttons")' "$warnings"
grep -Fq 'Dictionary<string, ZInput.ButtonDef>' "$warnings"
grep -Fq 'native.Value.GetActionPath()' "$warnings"
grep -Fq 'StringComparison.OrdinalIgnoreCase' "$warnings"

# Loadout Swap owns native Hide on R, but any other native action bound to R is
# still a warning. Valheim's single-key actions still fire while Shift is held,
# so the gameplay chords are compared by their underlying effective key path.
grep -Fq 'new Entry("R", "Swap hotbar loadout (replaces Hide weapons)")' "$catalog"
grep -Fq 'new("R", "Swap hotbar loadout", "<Keyboard>/r", ignoredNativeAction: "Hide")' "$catalog"
grep -Fq 'string.Equals(native.Key, binding.IgnoredNativeAction' "$warnings"
grep -Fq 'new("Left Shift + B", "Open the Benheim menu", "<Keyboard>/b")' "$catalog"
grep -Fq 'new("Left Shift + P", "Put matching items away", "<Keyboard>/p")' "$catalog"
if rg -n 'new\("(P|Backspace|Delete|Enter)"' "$catalog"; then
  printf 'inventory-only shortcuts must not produce gameplay binding warnings\n' >&2
  exit 1
fi

# The Controls-only block is absent unless a live collision exists and each row
# identifies the Benheim key/action and native action.
grep -Fq 'BuildControlsWarnings((RectTransform)controls.transform)' "$tabs"
grep -Fq 'controlsWarnings.SetActive(warnings.Count > 0)' "$warnings"
grep -Fq 'conflicts with native {warning.NativeAction}' "$warnings"
grep -Fq 'Positive gains are doubled; perfect defenses show the actual gain' "$catalog"
grep -Fq 'Baking and done-to-burn timing are halved; fuel stays normal' "$catalog"

printf 'shortcut collision warning source checks passed\n'
