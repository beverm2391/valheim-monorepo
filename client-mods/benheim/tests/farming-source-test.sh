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
assert_source 'size % 2 == 1' 'src/Farming/FarmingGridSelection.cs'
assert_source 'new List<FarmingGridPoint>\(size \* size\)' 'src/Farming/FarmingGrid.cs'
assert_source 'row == size / 2 && column == size / 2' 'src/Farming/FarmingGrid.cs'
assert_source 'position \+= left' 'src/Farming/FarmingGrid.cs'
assert_source 'rowOrigin \+= forward' 'src/Farming/FarmingGrid.cs'
assert_source 'PlantingDiagnostics\.PlacementFinished\(PlantingState\.GridSize' 'src/Farming/MassPlanting.cs'
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
placement_block="$(sed -n '/Piece selectedPiece = m_buildPieces.GetSelectedPiece()/,/if (TryPlacePiece(selectedPiece))/p' "$native_player")"
grep -Fq 'HaveStamina(rightItem.m_shared.m_attack.m_attackStamina)' <<<"$placement_block"
grep -Fq 'Each successful ordinary or grid plant placement costs 25% of the native planting stamina cost that Valheim has already resolved' "$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
grep -Fq 'Skipped, failed, and rejected placements cost no stamina' "$root/src/Shortcuts/ShortcutOverlayCatalog.cs"

# The clickable selector must not intercept native number keys or hotbar use.
if rg -n 'UseHotbarItem|"Hotbar|KeyCode\.(Alpha|Keypad)|typeof\(ZInput\)' "$root/src/Farming" --glob '*.cs'; then
  printf 'Farming must not intercept native number-key or hotbar input\n' >&2
  exit 1
fi

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
