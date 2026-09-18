#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Interaction/StationBuildCoveragePatch.cs"
native_tree="$($root/scripts/ensure-valheim-source.sh)"
native_station="$native_tree/CraftingStation.cs"
native_player="$native_tree/Player.cs"
native_hud="$native_tree/Hud.cs"

# Valheim resolves extension-aware station coverage with a horizontal distance
# check. All three Hammer surfaces call the same native lookup, while the area
# marker projector remains separate from the station's world-effect collider.
grep -Fq 'float stationBuildRange = allStation.GetStationBuildRange();' "$native_station"
grep -Fq 'Vector3.Distance(allStation.transform.position, point) < stationBuildRange' "$native_station"
grep -Fq 'm_buildRange = m_rangeBuild + (float)GetExtentionCount(checkExtensions: false) * m_extraRangePerLevel;' "$native_station"
grep -Fq 'm_areaMarkerCircle.m_radius = m_buildRange;' "$native_station"
grep -Fq 'public bool HaveRequirements(Piece piece, RequirementMode mode)' "$native_player"
grep -Fq 'else if (!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, base.transform.position)' "$native_player"
grep -Fq 'private bool CheckCanRemovePiece(Piece piece)' "$native_player"
grep -Fq '!CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, base.transform.position)' "$native_player"
grep -Fq 'if (!CheckCanRemovePiece(piece))' "$native_player"
grep -Fq 'private void SetupPieceInfo(Piece piece)' "$native_hud"
grep -Fq 'CraftingStation craftingStation = CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, localPlayer.transform.position);' "$native_hud"
grep -Fq 'craftingStation.ShowAreaMarker();' "$native_hud"

grep -Fq '[HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[]' "$source_file"
grep -Fq '[HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]' "$source_file"
grep -Fq '[HarmonyPatch(typeof(Hud), "SetupPieceInfo")]' "$source_file"
grep -Fq 'nameof(CraftingStation.HaveBuildStationInRange)' "$source_file"
grep -Fq 'nameof(StationBuildCoverage.FindForPlacement)' "$source_file"
grep -Fq 'nameof(StationBuildCoverage.FindForMaintenance)' "$source_file"
grep -Fq 'nameof(StationBuildCoverage.FindForHammerUi)' "$source_file"
grep -Fq 'internal const string WorkbenchName = "$piece_workbench";' "$source_file"
grep -Fq 'internal const string StonecutterName = "$piece_stonecutter";' "$source_file"
grep -Fq 'internal const string WorkbenchPrefab = "piece_workbench";' "$source_file"
grep -Fq 'internal const string StonecutterPrefab = "piece_stonecutter";' "$source_file"
grep -Fq 'CraftingStation? nativeStation = CraftingStation.HaveBuildStationInRange(name, point);' "$source_file"
grep -Fq 'float extendedRange = station.GetStationBuildRange() * Multiplier;' "$source_file"
grep -Fq 'CircleProjector? projector = station.m_areaMarker?.GetComponent<CircleProjector>();' "$source_file"
grep -Fq 'projector.m_radius = radius;' "$source_file"
grep -Fq 'hammer_station_range_state' "$source_file"
grep -Fq '.String("action", action)' "$source_file"
grep -Fq '.String("result", result)' "$source_file"

# The shared station lookup and station state stay native. Only the three
# Hammer-owned callers are rewritten, and only the visual projector is changed.
if grep -Fq '[HarmonyPatch(typeof(CraftingStation), nameof(CraftingStation.HaveBuildStationInRange))]' "$source_file"; then
  printf 'station build coverage must not patch the shared native lookup globally\n' >&2
  exit 1
fi

# The coherent Hammer patch must not mutate shared station range, use,
# extension, suppression, ward, comfort, spawn, or network state.
if rg -n 'm_rangeBuild\s*=|m_buildRange\s*=|m_extraRangePerLevel\s*=|m_useDistance\s*=|m_effectAreaCollider\s*=|EffectArea\.|PrivateArea\.|StationExtension\.|ZNetView|ZDO' "$source_file"; then
  printf 'station build coverage patch mutates state outside the Hammer work-zone seam\n' >&2
  exit 1
fi

printf 'station build coverage native-source checks passed\n'
