#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Shortcuts/ShortcutOverlay.cs"
content_file="$root/src/Shortcuts/ShortcutOverlayContent.cs"
patches_file="$root/src/Shortcuts/ShortcutOverlayInputPatches.cs"
templates_file="$root/src/Shortcuts/NativeTemplates.cs"
tabs_file="$root/src/Shortcuts/ShortcutOverlayTabs.cs"
catalog_file="$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
plugin="$root/src/Plugin.cs"
overlay_files=("$source_file" "$content_file" "$patches_file" "$templates_file" "$tabs_file" "$catalog_file")

if rg -n 'OnGUI|GUIStyle|GUILayout|GUI\.Label|Texture2D|PreloadTextOnce' "${overlay_files[@]}" "$plugin"; then
  printf 'shortcut panel must not retain the obsolete IMGUI implementation\n' >&2
  exit 1
fi

grep -Fq 'NativeTemplates.TryCreate()' "$content_file"
grep -Fq 'InventoryGui.instance' "$templates_file"
grep -Fq 'root/Crafting/Bkg' "$templates_file"
grep -Fq 'root/Container/TakeAll' "$templates_file"
grep -Fq 'root/Container/ContainerGrid' "$templates_file"
grep -Fq 'root/Container/ContainerScroll' "$templates_file"
grep -Fq 'root/Crafting/Decription/Description' "$templates_file"
grep -Fq 'root/Container/container_name' "$templates_file"
grep -Fq 'root/Container/TakeAll/Text' "$templates_file"
if rg -n 'Resources\.FindObjectsOfTypeAll|NativeCandidates|FindNativeComponent' "${overlay_files[@]}"; then
  printf 'shortcut panel must use exact InventoryGui style donors\n' >&2
  exit 1
fi
grep -Fq 'CopyImageStyle(templates.PanelBackground, window)' "$content_file"
grep -Fq 'overlayCanvas.overrideSorting = true' "$content_file"
grep -Fq 'overlayCanvas.sortingOrder = OverlaySortingOrder' "$content_file"
grep -Fq 'root.AddComponent<GraphicRaycaster>()' "$content_file"
grep -Fq 'TextMeshProUGUI' "$source_file"
grep -Fq 'text.font = template.font' "$source_file"
grep -Fq 'button.colors = templates.Button.colors' "$source_file"
grep -Fq 'ScrollRect' "$source_file"
grep -Fq 'Scrollbar' "$source_file"
grep -Fq 'VerticalLayoutGroup' "$tabs_file"
grep -Fq 'ContentSizeFitter' "$tabs_file"
grep -Fq 'closeButton.onClick.AddListener(Hide)' "$content_file"
grep -Fq 'MenuShortcutDown() || RawKeyDown(KeyCode.Escape)' "$source_file"
grep -Fq 'RawKeyDown(KeyCode.B)' "$source_file"
grep -Fq 'Input.GetKey(KeyCode.LeftShift)' "$source_file"
grep -Fq 'nextBuildAttemptAt = Time.unscaledTime + 1f' "$source_file"
grep -Fq 'ResetUiState(destroyRoot: false)' "$source_file"
grep -Fq 'ResetUiState(destroyRoot: true)' "$source_file"
grep -Fq 'ShortcutOverlayPlayerInputPatch' "$patches_file"
grep -Fq 'ShortcutOverlayMenuVisibilityPatch' "$patches_file"
grep -Fq 'if (!visible)' "$source_file"
grep -Fq 'InventoryTransactions.GetCapabilitySnapshot()' "$tabs_file"
grep -Fq 'player.PlayerName' "$tabs_file"
grep -Fq 'player.ClientVersion' "$tabs_file"
grep -Fq 'player.ProtocolVersion' "$tabs_file"
grep -Fq 'player.IsDetected' "$tabs_file"
grep -Fq 'player.IsCompatible' "$tabs_file"
grep -Fq 'multiplayerStatus.richText = false' "$tabs_file"
grep -Fq 'AddTab(buttons, templates, ShortcutTab.Controls, "Controls"' "$tabs_file"
grep -Fq 'AddTab(buttons, templates, ShortcutTab.Features, "Features"' "$tabs_file"
grep -Fq 'AddTab(buttons, templates, ShortcutTab.Multiplayer, "Multiplayer"' "$tabs_file"
grep -Fq 'new Entry("P", "Pocket the hovered stack or item")' "$catalog_file"
grep -Fq 'new Entry("Rockbreaker"' "$catalog_file"
grep -Fq 'new Entry("Cleave"' "$catalog_file"
grep -Fq 'new Entry("Adrenaline"' "$catalog_file"
grep -Fq 'keySize.preferredWidth = 230f' "$tabs_file"
grep -Fq 'multiplayerSummary.text = FormatMultiplayerSummary(snapshot)' "$tabs_file"
grep -Fq 'blocker.color = new Color(0f, 0f, 0f, 0.56f)' "$content_file"

grep -Fq '"Inventory"' "$catalog_file"
grep -Fq '"Crafting & Repair"' "$catalog_file"
grep -Fq '"Farming"' "$catalog_file"
grep -Fq '"World & Travel"' "$catalog_file"
grep -Fq '"Skills"' "$catalog_file"
grep -Fq 'new Entry("F7", "Save the active Benheim log to the Desktop")' "$catalog_file"
grep -Fq 'new Entry("Left Shift + station input", "Fill its available input or fuel capacity")' "$catalog_file"
grep -Fq 'Equipped and hotbar items stay protected without a marker.' "$catalog_file"
grep -Fq 'Stackables protect every stack of that item type; non-stackable gear protects only the marked item.' "$catalog_file"
grep -Fq 'Left Shift + B / Escape' "$content_file"
grep -Fq 'ShortcutOverlay.Destroy();' "$plugin"
grep -Fq 'RestoreCursor();' "$source_file"

for file in "${overlay_files[@]}"; do
  if [[ "$(wc -l < "$file")" -ge 450 ]]; then
    printf 'shortcut source exceeds the 449-line limit: %s\n' "$file" >&2
    exit 1
  fi
done

printf 'native shortcut menu, input blocking, and dynamic roster checks passed\n'
