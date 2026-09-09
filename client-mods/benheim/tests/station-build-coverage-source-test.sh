#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Interaction/StationBuildCoveragePatch.cs"
native_tree="$($root/scripts/ensure-valheim-source.sh)"
native_station="$native_tree/CraftingStation.cs"
native_player="$native_tree/Player.cs"

# Valheim resolves extension-aware station coverage with a horizontal distance
# check. Benheim reuses that shape only when Player.HaveRequirements evaluates
# whether the selected piece can be placed.
grep -Fq 'float stationBuildRange = allStation.GetStationBuildRange();' "$native_station"
grep -Fq 'Vector3.Distance(allStation.transform.position, point) < stationBuildRange' "$native_station"
grep -Fq 'm_buildRange = m_rangeBuild + (float)GetExtentionCount(checkExtensions: false) * m_extraRangePerLevel;' "$native_station"
grep -Fq 'public bool HaveRequirements(Piece piece, RequirementMode mode)' "$native_player"
grep -Fq 'else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, base.transform.position)' "$native_player"

grep -Fq '[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[]' "$source_file"
grep -Fq 'nameof(CraftingStation.HaveBuildStationInRange)' "$source_file"
grep -Fq 'nameof(StationBuildCoverage.FindForPlacement)' "$source_file"
grep -Fq 'internal const string WorkbenchName = "$piece_workbench";' "$source_file"
grep -Fq 'internal const string StonecutterName = "$piece_stonecutter";' "$source_file"
grep -Fq 'internal const string WorkbenchPrefab = "piece_workbench";' "$source_file"
grep -Fq 'internal const string StonecutterPrefab = "piece_stonecutter";' "$source_file"
grep -Fq 'CraftingStation? nativeStation = CraftingStation.HaveBuildStationInRange(name, point);' "$source_file"
grep -Fq 'float extendedRange = station.GetStationBuildRange() * Multiplier;' "$source_file"

# Removal and repair still route through the unpatched native lookup.
grep -Fq 'private bool CheckCanRemovePiece(Piece piece)' "$native_player"
grep -Fq '!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, base.transform.position)' "$native_player"
grep -Fq 'if (!CheckCanRemovePiece(piece))' "$native_player"
if grep -Fq '[HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.HaveBuildStationInRange))]' "$source_file"; then
  printf 'station build coverage must not patch the shared native lookup globally\n' >&2
  exit 1
fi

# The placement-only patch must not mutate the shared native station fields or
# name any adjacent gameplay systems.
if rg -n '__instance\.|m_rangeBuild\s*=|m_extraRangePerLevel\s*=|m_useDistance|m_effectAreaCollider|EffectArea|PrivateArea|StationExtension|CheckCanRemovePiece|Repair\(' "$source_file"; then
  printf 'station build coverage patch touches state outside the placement range seam\n' >&2
  exit 1
fi

printf 'station build coverage native-source checks passed\n'
