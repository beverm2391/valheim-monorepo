#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Shortcuts/ShortcutOverlay.cs"
content_file="$root/src/Shortcuts/ShortcutOverlayContent.cs"
patches_file="$root/src/Shortcuts/ShortcutOverlayInputPatches.cs"
templates_file="$root/src/Shortcuts/NativeTemplates.cs"
tabs_file="$root/src/Shortcuts/ShortcutOverlayTabs.cs"
catalog_file="$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
warnings_file="$root/src/Shortcuts/ShortcutOverlayWarnings.cs"
plugin="$root/src/Plugin.cs"
overlay_files=("$source_file" "$content_file" "$patches_file" "$templates_file" "$tabs_file" "$catalog_file" "$warnings_file")

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
grep -Fq 'AddTab(buttons, templates, ShortcutTab.Controls, "Controls"' "$tabs_file"
grep -Fq 'AddTab(buttons, templates, ShortcutTab.Features, "Features"' "$tabs_file"
grep -Fq 'new Entry("P", "Pocket the hovered stack or item")' "$catalog_file"
grep -Fq 'new Entry("R", "Swap hotbar loadout (replaces Hide weapons)")' "$catalog_file"
grep -Fq 'new Entry("Rockbreaker"' "$catalog_file"
grep -Fq 'new Entry("Cleave"' "$catalog_file"
grep -Fq '"Finewood"' "$catalog_file"
grep -Fq 'Native Birch, Oak, and Pine logs convert each final ordinary Wood drop to Finewood' "$catalog_file"
grep -Fq "without changing each log's native item count or Valheim's spawn path" "$catalog_file"
grep -Fq 'The compatible client that owns the log converts its drops' "$catalog_file"
grep -Fq 'including when another compatible client attacks' "$catalog_file"
grep -Fq 'Native Finewood, Core Wood, and other non-Wood drops, other logs, standing-tree drops, stumps, damage-type conversions, and unrelated destruction stay native.' "$catalog_file"
grep -Fq '"Headshots"' "$catalog_file"
grep -Fq '"Perfect Impact"' "$catalog_file"
grep -Fq 'AirborneMeleeTuning.ApproachSpeedThreshold' "$catalog_file"
grep -Fq 'AirborneMeleeTuning.DamageMultiplier' "$catalog_file"
grep -Fq 'AirborneMeleeTuning.StaggerMultiplier' "$catalog_file"
grep -Fq 'approach the contact horizontally' "$catalog_file"
grep -Fq '"Tar-pit pickup"' "$catalog_file"
grep -Fq "Manually pick up ordinary items submerged in native tar pits, or collect them with Valheim's normal auto-pickup" "$catalog_file"
grep -Fq 'Use Feasts and other interactable objects at up to 8 m. Open containers remain available at up to 10 m' "$catalog_file"
grep -Fq '"Glowing signs"' "$catalog_file"
grep -Fq 'Existing sign letters have a soft, warm portal-amber glow. The wooden board stays unchanged' "$catalog_file"
grep -Fq '"Portal labels"' "$catalog_file"
grep -Fq 'A visual-only Valheim wooden sign board floats 20–30 cm above each tagged wooden or stone portal, stays fixed to portal rotation instead of billboarding, shows the tag on both sides with glowing sign letters, and is naturally occluded by scene geometry' "$catalog_file"
if grep -Fq 'WorldLabelVisibility' "$catalog_file"; then
  printf 'shortcut catalog must not depend on retired world-text policy\n' >&2
  exit 1
fi
grep -Fq '"Building"' "$catalog_file"
grep -Fq '"Station coverage"' "$catalog_file"
grep -Fq 'Workbench and Stonecutter build-piece placement coverage is 2× Valheim' "$catalog_file"
grep -Fq '20 m to 40 m for level-1 stations' "$catalog_file"
grep -Fq 'Workbench suppression, enemy spawning, and all other station behavior stay native.' "$catalog_file"
grep -Fq '"Ship Sprint"' "$catalog_file"
grep -Fq 'ShipSprintTuning.ThrustMultiplier' "$catalog_file"
grep -Fq '"Combat"' "$catalog_file"
grep -Fq 'HeadshotRules.NearMultiplier' "$catalog_file"
grep -Fq 'Native WeakSpot hits stay native.' "$catalog_file"
grep -Fq 'one half-damage hit to the same tree or log' "$catalog_file"
grep -Fq 'Positive gains are doubled; perfect defenses show the actual gain' "$catalog_file"
grep -Fq 'new Entry(' "$catalog_file"
grep -Fq '"CLUTCH"' "$catalog_file"
grep -Fq '"UNTOUCHABLE"' "$catalog_file"
grep -Fq 'perfect defenses or qualifying kills' "$catalog_file"
grep -Fq '"BERSERKER"' "$catalog_file"
grep -Fq '"SLAUGHTERHOUSE"' "$catalog_file"
grep -Fq 'KillChainRules.BerserkerKillThreshold' "$catalog_file"
grep -Fq 'KillChainRules.SlaughterhouseKillThreshold' "$catalog_file"
grep -Fq 'KillChainRules.WindowSeconds' "$catalog_file"
grep -Fq 'BERSERKER, SLAUGHTERHOUSE, and kill-based UNTOUCHABLE progression require Benheim Server Support' "$catalog_file"
grep -Fq 'Baking and done-to-burn timing are halved; fuel stays normal' "$catalog_file"
grep -Fq 'keySize.preferredWidth = 230f' "$tabs_file"
grep -Fq 'blocker.color = new Color(0f, 0f, 0f, 0.56f)' "$content_file"

if rg -n 'InventoryTransaction|InventoryCapability|Multiplayer' "${overlay_files[@]}"; then
  printf 'shortcut panel must not retain Put Away protocol status UI\n' >&2
  exit 1
fi

grep -Fq '"Inventory"' "$catalog_file"
grep -Fq '"Crafting & Repair"' "$catalog_file"
grep -Fq '"Farming"' "$catalog_file"
grep -Fq 'FarmingSettings.DefaultGridSize' "$catalog_file"
grep -Fq 'each time the picker opens' "$catalog_file"
grep -Fq '"Cultivator berries"' "$catalog_file"
grep -Fq 'PlantableBerries.BerryCost' "$catalog_file"
grep -Fq '"Hammer berries"' "$catalog_file"
grep -Fq 'native access and ward rules allow it' "$catalog_file"
grep -Fq 'Naturally spawned bushes cannot be removed with the Hammer' "$catalog_file"
grep -Fq 'the Cultivator removes no berry bushes' "$catalog_file"
grep -Fq 'PlantableBerries.BerryRespawnMinimumSeconds' "$catalog_file"
grep -Fq 'PlantableBerries.BerryRespawnMaximumSeconds' "$catalog_file"
grep -Fq 'Benheim assigns native Raspberry, Blueberry, and Cloudberry bushes' "$catalog_file"
grep -Fq 'Newly planted bushes start empty.' "$catalog_file"
grep -Fq '"World & Travel"' "$catalog_file"
grep -Fq '"Gathering & Skills"' "$catalog_file"
grep -Fq '"Affinities"' "$catalog_file"
grep -Fq '"Club + Lunge"' "$catalog_file"
grep -Fq 'AffinityPresentation.RequirementsFor(AffinityLoadResult.Lunge).MaterialAmount' "$catalog_file"
grep -Fq 'AffinityPresentation.RequirementsFor(AffinityLoadResult.Snipe).MaterialAmount' "$catalog_file"
grep -Fq 'AffinityPresentation.RequirementsFor(AffinityLoadResult.Test).MaterialAmount' "$catalog_file"
grep -Fq 'At a level-1 Forge' "$catalog_file"
grep -Fq '"Airborne swing"' "$catalog_file"
grep -Fq 'LungeRuntime.DefaultForce' "$catalog_file"
grep -Fq 'LungeRuntime.MinimumVerticalVelocity' "$catalog_file"
grep -Fq 'grounded Club swings stay native' "$catalog_file"
grep -Fq 'Replacing an Affinity destroys the old one and all materials previously spent on it' "$catalog_file"
grep -Fq '"Combat"' "$catalog_file"
grep -Fq 'new Entry("F7", "Save the active Benheim log to the Desktop")' "$catalog_file"
grep -Fq '"Earned-state cue"' "$catalog_file"
grep -Fq '"Compatible nearby players may hear the native charm audio; distant players cannot"' "$catalog_file"
grep -Fq 'controlsWarnings.SetActive(warnings.Count > 0)' "$warnings_file"
grep -Fq 'conflicts with native {EscapeMarkup(warning.NativeAction)}' "$warnings_file"
grep -Fq 'new Entry("Left Shift + hammer repair", $"Repair eligible buildings and structures within {BuildingRepair.RepairRadius:0.#} m")' "$catalog_file"
grep -Fq 'new Entry("Left Shift + station input", "Fill its available input or fuel capacity")' "$catalog_file"
grep -Fq 'Equipped and hotbar items stay protected without a marker.' "$catalog_file"
grep -Fq 'Place stacks, Hold to stack, and Put Away keep protected items with you.' "$catalog_file"
grep -Fq 'Stackables protect every stack of that item type; non-stackable gear protects only the marked item.' "$catalog_file"
grep -Fq 'Left Shift + B / Escape' "$content_file"
grep -Fq 'ShortcutOverlay.Destroy();' "$plugin"
grep -Fq 'RestoreCursor();' "$source_file"

# The config panel describes every effect controlled by Combat Shake.
grep -Fq 'Cleave, mining AOE, and Perfect Impact' "$root/src/Shortcuts/ShortcutOverlayConfig.cs"

for file in "${overlay_files[@]}"; do
  if [[ "$(wc -l < "$file")" -ge 450 ]]; then
    printf 'shortcut source exceeds the 449-line limit: %s\n' "$file" >&2
    exit 1
  fi
done

printf 'native shortcut menu and input blocking checks passed\n'
