#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

assert_source() {
  local pattern="$1"
  local file="$2"
  if ! grep -Eq "$pattern" "$root/$file"; then
    printf 'missing expected source pattern %s in %s\n' "$pattern" "$file" >&2
    exit 1
  fi
}

assert_source 'HarvestRadius = 10f' 'src/Farming/FarmingSettings.cs'
assert_source 'GridWidth = 5' 'src/Farming/FarmingSettings.cs'
assert_source 'GridLength = 5' 'src/Farming/FarmingSettings.cs'
assert_source 'row == FarmingSettings.GridLength / 2' 'src/Farming/FarmingGrid.cs'
assert_source 'column == FarmingSettings.GridWidth / 2' 'src/Farming/FarmingGrid.cs'
assert_source 'GetGlobalKey\(anchorPiece.FreeBuildKey\(\)\)' 'src/Farming/MassPlanting.cs'
assert_source 'ApplyBuildSkill\(player, pieceTable\)' 'src/Farming/MassPlanting.cs'
assert_source 'InputState\.IsTextEntryActive\(\)' 'src/Farming/FarmingInput.cs'
assert_source 'Left Shift \+ interact' 'src/Shortcuts/ShortcutOverlayContent.cs'
assert_source 'Left Shift \+ plant' 'src/Shortcuts/ShortcutOverlayContent.cs'
assert_source 'MassFarming v1\.12' 'THIRD_PARTY_NOTICES.md'

if grep -Rqs 'xeio\.MassFarming' "$root/src"; then
  printf 'BenheimQoL source still references the separate MassFarming plugin\n' >&2
  exit 1
fi

printf 'farming source checks passed\n'
