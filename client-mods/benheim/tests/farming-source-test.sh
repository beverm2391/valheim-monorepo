#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_player="$source_tree/Player.cs"
native_version="$source_tree/Version.cs"
mass_planting="$root/src/Farming/MassPlanting.cs"

assert_source() {
  local pattern="$1"
  local file="$2"
  if ! grep -Eq "$pattern" "$root/$file"; then
    printf 'missing expected source pattern %s in %s\n' "$pattern" "$file" >&2
    exit 1
  fi
}

assert_source 'HarvestRadius = 10f' 'src/Farming/FarmingSettings.cs'
assert_source 'DefaultGridSize = 9' 'src/Farming/FarmingSettings.cs'
assert_source 'size % 2 == 1' 'src/Farming/FarmingGridSelection.cs'
assert_source 'new List<FarmingGridPoint>\(size \* size\)' 'src/Farming/FarmingGrid.cs'
assert_source 'row == size / 2 && column == size / 2' 'src/Farming/FarmingGrid.cs'
assert_source 'position \+= left' 'src/Farming/FarmingGrid.cs'
assert_source 'rowOrigin \+= forward' 'src/Farming/FarmingGrid.cs'
assert_source 'HarmonyPatch\(typeof\(Player\), "Update"\)' 'src/Farming/FarmingPatches.cs'
assert_source 'UpdateGridSelection\(__instance\)' 'src/Farming/FarmingPatches.cs'
assert_source 'HarmonyPatch\(typeof\(Player\), "UseHotbarItem"\)' 'src/Farming/FarmingPatches.cs'
assert_source 'ShouldSuppressHotbarUse\(__instance, index\)' 'src/Farming/FarmingPatches.cs'
assert_source 'Hud\.IsPieceSelectionVisible\(\)' 'src/Farming/FarmingInput.cs'
assert_source 'rightItem\.m_dropPrefab\.name == "Cultivator"' 'src/Farming/FarmingInput.cs'
assert_source 'Time\.frameCount == suppressedHotbarFrame' 'src/Farming/FarmingInput.cs'
assert_source 'InputState\.IsKeyDown\(alpha\)' 'src/Farming/FarmingInput.cs'
assert_source 'candidate \+= 2' 'src/Farming/FarmingInput.cs'
assert_source 'IsAnotherModifierHeld\(\)' 'src/Farming/FarmingInput.cs'
assert_source 'KeyCode\.AltGr' 'src/Farming/FarmingInput.cs'
assert_source 'KeyCode\.LeftCommand' 'src/Farming/FarmingInput.cs'
assert_source 'KeyCode\.RightCommand' 'src/Farming/FarmingInput.cs'
assert_source 'KeyCode\.LeftWindows' 'src/Farming/FarmingInput.cs'
assert_source 'KeyCode\.RightWindows' 'src/Farming/FarmingInput.cs'
assert_source 'ShouldHandleInput\(' 'src/Farming/FarmingInput.cs'
assert_source 'UpdatePickerSession\(pickerOpen\)' 'src/Farming/FarmingInput.cs'
assert_source 'PlantingPreview\.DestroyGhosts\(\)' 'src/Farming/FarmingInput.cs'
assert_source 'FarmingInput\.ResetGridSelection\(\)' 'src/Plugin.cs'
assert_source 'grid=\{PlantingState\.GridSize\}x\{PlantingState\.GridSize\}' 'src/Farming/MassPlanting.cs'
assert_source 'GetGlobalKey\(anchorPiece.FreeBuildKey\(\)\)' 'src/Farming/MassPlanting.cs'
assert_source 'ApplyBuildSkill\(player, pieceTable\)' 'src/Farming/MassPlanting.cs'
assert_source 'CostMultiplier = 0\.25f' 'src/Farming/PlantingStamina.cs'
assert_source 'return nativeCost \* CostMultiplier' 'src/Farming/PlantingStamina.cs'
assert_source 'HarmonyPatch\(typeof\(Player\), "GetBuildStamina"\)' 'src/Farming/PlantingStaminaPatches.cs'
assert_source 'ApplyResolvedCost\(___m_buildPieces, ref __result\)' 'src/Farming/PlantingStaminaPatches.cs'
assert_source 'HarmonyPatch\(typeof\(Player\), "UpdatePlacement"\)' 'src/Farming/PlantingStaminaPatches.cs'
assert_source 'PlantingStamina\.HasPlacementStamina' 'src/Farming/PlantingStaminaPatches.cs'
assert_source 'return player\.HaveStamina\(FarmingReflection\.GetBuildStamina\(player\)\)' 'src/Farming/PlantingStamina.cs'
assert_source 'InputState\.IsTextEntryActive\(\)' 'src/Farming/FarmingInput.cs'
assert_source 'FarmingGridSelection\.CurrentSize\)' 'src/Farming/PlantingPreview.cs'
assert_source 'GridSize = FarmingGridSelection\.CurrentSize' 'src/Farming/PlantingState.cs'
assert_source 'PlantingState\.GridSize\)' 'src/Farming/MassPlanting.cs'
assert_source 'Left Shift \+ interact' 'src/Shortcuts/ShortcutOverlayCatalog.cs'
assert_source 'Left Shift \+ plant' 'src/Shortcuts/ShortcutOverlayCatalog.cs'
assert_source 'MassFarming v1\.12' 'THIRD_PARTY_NOTICES.md'

grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$native_version"
grep -Fq 'if (TryPlacePiece(selectedPiece))' "$native_player"
grep -Fq 'UseStamina(GetBuildStamina());' "$native_player"
grep -Fq 'private float GetBuildStamina()' "$native_player"
for hotbar in {1..8}; do
  grep -Fq "ZInput.GetButtonDown(\"Hotbar${hotbar}\")" "$native_player"
  grep -Fq "UseHotbarItem(${hotbar});" "$native_player"
done
if grep -Fq 'ZInput.GetButtonDown("Hotbar9")' "$native_player"; then
  printf 'native Player.Update unexpectedly gained Hotbar9; revisit the direct 9-key seam\n' >&2
  exit 1
fi
placement_block="$(sed -n '/Piece selectedPiece = m_buildPieces.GetSelectedPiece()/,/if (TryPlacePiece(selectedPiece))/p' "$native_player")"
grep -Fq 'HaveStamina(rightItem.m_shared.m_attack.m_attackStamina)' <<<"$placement_block"
grep -Fq 'Each successful ordinary or grid plant placement costs 25% of the native planting stamina cost that Valheim has already resolved' "$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
grep -Fq 'Skipped, failed, and rejected placements cost no stamina' "$root/src/Shortcuts/ShortcutOverlayCatalog.cs"

# Grid placement reaches its only stamina debit after every rejection and after
# the successful placement call. Skipped, failed, and rejected positions are free.
test "$(grep -Fc 'player.UseStamina(staminaCost);' "$mass_planting")" -eq 1
place_line="$(grep -nF 'player.PlacePiece(anchorPiece' "$mass_planting" | cut -d: -f1)"
stamina_line="$(grep -nF 'player.UseStamina(staminaCost);' "$mass_planting" | cut -d: -f1)"
test "$stamina_line" -gt "$place_line"

dotnet run --project "$root/tests/native-mechanic-transpilers/NativeMechanicTranspilerTests.csproj"
dotnet run --project "$root/tests/farming-grid-selection/FarmingGridSelectionTests.csproj"

if grep -Rqs 'xeio\.MassFarming' "$root/src"; then
  printf 'BenheimQoL source still references the separate MassFarming plugin\n' >&2
  exit 1
fi

printf 'farming source checks passed\n'
