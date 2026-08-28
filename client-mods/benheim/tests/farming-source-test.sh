#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_player="$source_tree/Player.cs"
native_version="$source_tree/Version.cs"

assert_source() {
  local pattern="$1"
  local file="$2"
  if ! grep -Eq "$pattern" "$root/$file"; then
    printf 'missing expected source pattern %s in %s\n' "$pattern" "$file" >&2
    exit 1
  fi
}

assert_source 'HarvestRadius = 10f' 'src/Farming/FarmingSettings.cs'
assert_source 'GridWidth = 9' 'src/Farming/FarmingSettings.cs'
assert_source 'GridLength = 9' 'src/Farming/FarmingSettings.cs'
assert_source 'new List<FarmingGridPoint>\(FarmingSettings.GridWidth \* FarmingSettings.GridLength\)' 'src/Farming/FarmingGrid.cs'
assert_source 'row == FarmingSettings.GridLength / 2' 'src/Farming/FarmingGrid.cs'
assert_source 'column == FarmingSettings.GridWidth / 2' 'src/Farming/FarmingGrid.cs'
assert_source 'position \+= left' 'src/Farming/FarmingGrid.cs'
assert_source 'rowOrigin \+= forward' 'src/Farming/FarmingGrid.cs'
assert_source 'GetGlobalKey\(anchorPiece.FreeBuildKey\(\)\)' 'src/Farming/MassPlanting.cs'
assert_source 'ApplyBuildSkill\(player, pieceTable\)' 'src/Farming/MassPlanting.cs'
assert_source 'CostMultiplier = 0\.5f' 'src/Farming/PlantingStamina.cs'
assert_source 'return nativeCost \* CostMultiplier' 'src/Farming/PlantingStamina.cs'
assert_source 'HarmonyPatch\(typeof\(Player\), "GetBuildStamina"\)' 'src/Farming/PlantingStaminaPatches.cs'
assert_source 'ApplyResolvedCost\(___m_buildPieces, ref __result\)' 'src/Farming/PlantingStaminaPatches.cs'
assert_source 'HarmonyPatch\(typeof\(Player\), "UpdatePlacement"\)' 'src/Farming/PlantingStaminaPatches.cs'
assert_source 'PlantingStamina\.HasPlacementStamina' 'src/Farming/PlantingStaminaPatches.cs'
assert_source 'return player\.HaveStamina\(FarmingReflection\.GetBuildStamina\(player\)\)' 'src/Farming/PlantingStamina.cs'
assert_source 'InputState\.IsTextEntryActive\(\)' 'src/Farming/FarmingInput.cs'
assert_source 'Left Shift \+ interact' 'src/Shortcuts/ShortcutOverlayCatalog.cs'
assert_source 'Left Shift \+ plant' 'src/Shortcuts/ShortcutOverlayCatalog.cs'
assert_source 'MassFarming v1\.12' 'THIRD_PARTY_NOTICES.md'

grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$native_version"
grep -Fq 'if (TryPlacePiece(selectedPiece))' "$native_player"
grep -Fq 'UseStamina(GetBuildStamina());' "$native_player"
grep -Fq 'private float GetBuildStamina()' "$native_player"
placement_block="$(sed -n '/Piece selectedPiece = m_buildPieces.GetSelectedPiece()/,/if (TryPlacePiece(selectedPiece))/p' "$native_player")"
grep -Fq 'HaveStamina(rightItem.m_shared.m_attack.m_attackStamina)' <<<"$placement_block"
grep -Fq 'Successful planting costs 50% of Valheim' "$root/src/Shortcuts/ShortcutOverlayCatalog.cs"

dotnet run --project "$root/tests/native-mechanic-transpilers/NativeMechanicTranspilerTests.csproj"

if grep -Rqs 'xeio\.MassFarming' "$root/src"; then
  printf 'BenheimQoL source still references the separate MassFarming plugin\n' >&2
  exit 1
fi

printf 'farming source checks passed\n'
