#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patch="$root/src/Interaction/ComfortFurnitureRangePatch.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native="$source_tree/SE_Rested.cs"

# Valheim 0.221.12 owns the complete comfort calculation. Its isolated helper
# passes the native 10-meter radius directly to the comfort-piece query.
grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$source_tree/Version.cs"
grep -Fq 'private static List<Piece> GetNearbyComfortPieces(Vector3 point)' "$native"
grep -Fq 'Piece.GetAllComfortPiecesInRadius(point, 10f, s_tempPieces);' "$native"

# Benheim patches only that helper's single radius constant and refuses to load
# the patch if the installed method no longer has exactly one matching value.
grep -Fq '[HarmonyPatch(typeof(SE_Rested), "GetNearbyComfortPieces")]' "$patch"
grep -Fq 'internal const float NativeComfortRadius = 10f;' "$patch"
grep -Fq 'internal const float ExtendedComfortRadius = 20f;' "$patch"
grep -Fq 'code.operand = ExtendedComfortRadius;' "$patch"
grep -Fq 'if (replaced != 1)' "$patch"

if rg -n 'CalculateComfortLevel|m_comfort|m_comfortGroup|GetComfort|InShelter|m_baseTTL|m_TTLPerComfortLevel|ZNet|ZDO|RPC' "$patch"; then
  printf 'comfort range patch must not replace native comfort or Rested behavior\n' >&2
  exit 1
fi

printf 'comfort furniture range source checks passed\n'
