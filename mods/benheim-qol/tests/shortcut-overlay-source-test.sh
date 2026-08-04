#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Shortcuts/ShortcutOverlay.cs"
content_file="$root/src/Shortcuts/ShortcutOverlayContent.cs"
patches_file="$root/src/Shortcuts/ShortcutOverlayInputPatches.cs"
plugin="$root/src/Plugin.cs"
overlay_files=("$source_file" "$content_file" "$patches_file")

if rg -n 'OnGUI|GUIStyle|GUILayout|GUI\.Label|Texture2D|PreloadTextOnce' "${overlay_files[@]}" "$plugin"; then
  printf 'shortcut panel must not retain the obsolete IMGUI implementation\n' >&2
  exit 1
fi

grep -Fq 'FindNativeTemplates()' "$source_file"
grep -Fq 'FindNativeCanvas()' "$source_file"
grep -Fq 'CopyImageStyle(templates.PanelBackground, window)' "$content_file"
grep -Fq 'TextMeshProUGUI' "$source_file"
grep -Fq 'text.font = template.font' "$source_file"
grep -Fq 'button.colors = templates.Button.colors' "$source_file"
grep -Fq 'ScrollRect' "$source_file"
grep -Fq 'Scrollbar' "$source_file"
grep -Fq 'VerticalLayoutGroup' "$source_file"
grep -Fq 'ContentSizeFitter' "$source_file"
grep -Fq 'closeButton.onClick.AddListener(Hide)' "$content_file"
grep -Fq 'RawKeyDown(KeyCode.F8) || RawKeyDown(KeyCode.Escape)' "$source_file"
grep -Fq 'ShortcutOverlayPlayerInputPatch' "$patches_file"
grep -Fq 'ShortcutOverlayMenuVisibilityPatch' "$patches_file"
grep -Fq 'if (!visible)' "$source_file"
grep -Fq 'InventoryTransactions.GetCapabilitySnapshot()' "$content_file"
grep -Fq 'Server — Benheim Inventory' "$content_file"
grep -Fq 'player.PlayerName' "$content_file"
grep -Fq 'player.ClientVersion' "$content_file"
grep -Fq 'player.ProtocolVersion' "$content_file"
grep -Fq 'player.IsDetected' "$content_file"
grep -Fq 'player.IsCompatible' "$content_file"
grep -Fq 'multiplayerStatus.richText = false' "$content_file"

grep -Fq '"Inventory"' "$content_file"
grep -Fq '"Build & Repair"' "$content_file"
grep -Fq '"Farming"' "$content_file"
grep -Fq '"Travel"' "$content_file"
grep -Fq '"Combat & Skills"' "$content_file"
grep -Fq '"Help"' "$content_file"
grep -Fq 'new Entry("F7", "Save a diagnostic log to the Desktop")' "$content_file"
grep -Fq 'new Entry("Left Shift + station input", "Fill its available input or fuel capacity")' "$content_file"
grep -Fq 'Stackables protect their item type; gear protects only the marked item.' "$content_file"
grep -Fq 'ShortcutOverlay.Destroy();' "$plugin"
grep -Fq 'RestoreCursor();' "$source_file"

for file in "${overlay_files[@]}"; do
  if [[ "$(wc -l < "$file")" -ge 450 ]]; then
    printf 'shortcut source exceeds the 449-line limit: %s\n' "$file" >&2
    exit 1
  fi
done

printf 'native shortcut menu, input blocking, and dynamic roster checks passed\n'
