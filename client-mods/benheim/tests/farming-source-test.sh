#!/usr/bin/env bash
set -euo pipefail
trap 'printf "farming source check failed at line %s: %s\n" "$LINENO" "$BASH_COMMAND" >&2' ERR

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_player="$source_tree/Player.cs"
native_version="$source_tree/Version.cs"
native_terrain_op="$source_tree/TerrainOp.cs"
mass_planting="$root/src/Farming/MassPlanting.cs"
grid_picker_view="$root/src/Farming/FarmingGridPickerView.cs"

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
assert_source 'ItemCheated\(anchorPiece\.m_resources\)' 'src/Farming/MassPlanting.cs'
assert_source 'NoCostCheat\(\)' 'src/Farming/MassPlanting.cs'
assert_source 'PlayerProfile\.s_bypassCheatChecks' 'src/Farming/MassPlanting.cs'
assert_source 'cheated: cheated' 'src/Farming/MassPlanting.cs'
assert_source 'IncrementStatBuildPiecePlaced\(anchorPiece\.m_name, 1f, cheated\)' 'src/Farming/MassPlanting.cs'
assert_source 'GetPlaceDurability\(player, tool\) \* Game\.m_durabilityRate' 'src/Farming/MassPlanting.cs'
assert_source 'InputState\.IsTextEntryActive\(\)' 'src/Farming/FarmingInput.cs'
assert_source 'FarmingGridSelection\.CurrentSize\)' 'src/Farming/PlantingPreview.cs'
assert_source 'GridSize = FarmingGridSelection\.CurrentSize' 'src/Farming/PlantingState.cs'
assert_source 'PlantingState\.GridSize\)' 'src/Farming/MassPlanting.cs'
assert_source 'BuildUi\? buildUi = hud\.m_buildUi' 'src/Farming/FarmingGridPickerView.cs'
assert_source 'AccessTools\.Field\(typeof\(BuildUi\), "m_pieceView"\)' 'src/Farming/FarmingGridPickerView.cs'
assert_source 'InventoryGui\.instance\?\.m_takeAllButton' 'src/Farming/FarmingGridPickerView.cs'
assert_source 'Hud\.CloseBuildUi\(\)' 'src/Farming/FarmingGridPicker.cs'
assert_source 'RadiusMultiplier = 3f' 'src/Farming/HoeRadiusExpansion.cs'
assert_source 'tool\.m_dropPrefab\.name != "Hoe"' 'src/Farming/HoeRadiusExpansion.cs'
assert_source 'TerrainOp\.m_forceDisableTerrainOps' 'src/Farming/HoeRadiusExpansion.cs'
assert_source 'typeof\(TerrainOp\.Settings\), nameof\(TerrainOp\.Settings\.Serialize\)' 'src/Farming/HoeRadiusPatches.cs'
assert_source 'typeof\(TerrainOp\.Settings\), nameof\(TerrainOp\.Settings\.Deserialize\)' 'src/Farming/HoeRadiusPatches.cs'
assert_source 'transform\.Find\("_GhostOnly"\)' 'src/Farming/HoeRadiusPreview.cs'
assert_source 'Left Shift \+ interact' 'src/Shortcuts/ShortcutOverlayCatalog.cs'
assert_source 'Left Shift \+ plant' 'src/Shortcuts/ShortcutOverlayCatalog.cs'
assert_source 'MassFarming v1\.12' 'THIRD_PARTY_NOTICES.md'

# Qualified against the installed 1.0.20 source. A new game version needs the
# placement-cost and terrain-protocol seams below inspected again.
if ! grep -Fq 'CurrentVersion { get; } = new GameVersion(1, 0, 20);' "$native_version"; then
  printf 'farming: native contracts are qualified for Valheim 1.0.20; inspect the installed source before updating this gate\n' >&2
  exit 1
fi
grep -Fq 'private void UpdatePlacement(bool takeInput, float dt)' "$native_player"
grep -Fq 'public bool TryPlacePiece(Piece piece)' "$native_player"
grep -Fq 'public void PlacePiece(Piece piece, Vector3 pos, Quaternion rot, bool doAttack = true, bool cheated = false)' "$native_player"
grep -Fq 'if (TryPlacePiece(selectedPiece))' "$native_player"
grep -Fq 'UseStamina(GetBuildStamina());' "$native_player"
grep -Fq 'private float GetBuildStamina()' "$native_player"
grep -Fq 'private float GetPlaceDurability(ItemDrop.ItemData tool)' "$native_player"
grep -Fq 'attackStamina *= 1f + GetEquipmentHomeItemModifier();' "$native_player"
grep -Fq 'm_seman.ModifyHomeItemStaminaUsage(attackStamina, ref attackStamina);' "$native_player"
grep -Fq 'attackStamina -= attackStamina * 0.5f * skillFactor;' "$native_player"
grep -Fq 'public void Serialize(ZPackage pkg, GameObject prefab)' "$native_terrain_op"
grep -Fq 'public static Settings Deserialize(ZPackage pkg)' "$native_terrain_op"
grep -Fq 'pkg.Write(prefabName.GetStableHashCode());' "$native_terrain_op"
grep -Fq 'return prefab.m_settings;' "$native_terrain_op"
grep -Fq 'modifier.m_settings.Serialize(zPackage, modifier.gameObject);' "$source_tree/TerrainComp.cs"
grep -Fq 'private void DoOperation(Vector3 pos, Vector3 rot, TerrainOp.Settings modifier)' "$source_tree/TerrainComp.cs"
grep -Fq 'private void UpdatePlacementGhost(bool flashGuardStone)' "$native_player"
grep -Fq 'public BuildUi m_buildUi;' "$source_tree/Hud.cs"
grep -Fq 'public static void CloseBuildUi()' "$source_tree/Hud.cs"
grep -Fq 'private RectTransform m_pieceView;' "$source_tree/BuildUi.cs"
placement_block="$(sed -n '/Piece selectedPiece = m_buildPieces.GetSelectedPiece()/,/if (TryPlacePiece(selectedPiece))/p' "$native_player")"
grep -Fq 'HaveStamina(rightItem.m_shared.m_attack.m_attackStamina)' <<<"$placement_block"
grep -Fq 'Each successful ordinary or grid plant placement costs 25% of the native planting stamina cost that Valheim has already resolved' "$root/src/Shortcuts/ShortcutOverlayCatalog.cs"
grep -Fq 'Skipped, failed, and rejected placements cost no stamina' "$root/src/Shortcuts/ShortcutOverlayCatalog.cs"

# The clickable selector must not intercept native number keys or hotbar use.
if rg -n 'UseHotbarItem|"Hotbar|KeyCode\.(Alpha|Keypad)|typeof\(ZInput\)' "$root/src/Farming" --glob '*.cs'; then
  printf 'Farming must not intercept native number-key or hotbar input\n' >&2
  exit 1
fi

# Radius expansion changes only the transient terrain settings and their
# preview. The native placement path continues to own the one action's costs.
if rg -n 'ConsumeResources|UseStamina|m_durability' "$root/src/Farming/HoeRadius"*.cs; then
  printf 'Hoe radius code must not duplicate native action costs\n' >&2
  exit 1
fi

# Valheim 1.0 moved the active picker into BuildUi. The retired Hud field must
# not return as the row's parent.
if rg -n 'hud\.m_pieceSelectionWindow' "$grid_picker_view"; then
  printf 'Farming grid picker still depends on the retired Hud picker\n' >&2
  exit 1
fi

# Grid placement reaches its only stamina debit after every rejection and after
# the successful placement call. Skipped, failed, and rejected positions are free.
test "$(grep -Fc 'player.UseStamina(staminaCost);' "$mass_planting")" -eq 1
place_line="$(grep -nF 'player.PlacePiece(' "$mass_planting" | cut -d: -f1)"
stamina_line="$(grep -nF 'player.UseStamina(staminaCost);' "$mass_planting" | cut -d: -f1)"
test "$stamina_line" -gt "$place_line"

dotnet run --project "$root/tests/farming-grid-selection/FarmingGridSelectionTests.csproj"
dotnet run --project "$root/tests/hoe-radius/HoeRadiusTests.csproj"

if grep -Rqs 'xeio\.MassFarming' "$root/src"; then
  printf 'BenheimQoL source still references the separate MassFarming plugin\n' >&2
  exit 1
fi

printf 'farming source checks passed\n'
