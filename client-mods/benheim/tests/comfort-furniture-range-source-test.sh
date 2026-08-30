#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patch="$root/src/Interaction/ComfortFurnitureRangePatch.cs"
capture="$root/src/Interaction/ComfortDiagnosticCapture.cs"
command="$root/src/Interaction/ComfortDiagnosticCommand.cs"
summary="$root/src/Interaction/ComfortDiagnosticSummary.cs"
client="$root/src/EnemyTiers/BenheimTestCommandClient.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native="$source_tree/SE_Rested.cs"
piece="$source_tree/Piece.cs"

# Valheim 0.221.12 owns the complete comfort calculation. Its isolated helper
# passes the native 10-meter radius directly to the comfort-piece query.
grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$source_tree/Version.cs"
grep -Fq 'private static List<Piece> GetNearbyComfortPieces(Vector3 point)' "$native"
grep -Fq 'Piece.GetAllComfortPiecesInRadius(point, 10f, s_tempPieces);' "$native"
grep -Fq 'nearbyComfortPieces.Sort(PieceComfortSort);' "$native"
grep -Fq 'num += piece.GetComfort();' "$native"
grep -Fq 'public static void GetAllComfortPiecesInRadius(Vector3 p, float radius, List<Piece> pieces)' "$piece"
grep -Fq 'Vector3.Distance(p, s_allComfortPiece.transform.position) < radius' "$piece"

# Benheim patches only that helper's single radius constant and refuses to load
# the patch if the installed method no longer has exactly one matching value.
grep -Fq '[HarmonyPatch(typeof(SE_Rested), "GetNearbyComfortPieces")]' "$patch"
grep -Fq 'internal const float NativeComfortRadius = 10f;' "$patch"
grep -Fq 'internal const float ExtendedComfortRadius = 20f;' "$patch"
grep -Fq 'code.operand = ExtendedComfortRadius;' "$patch"
grep -Fq 'ComfortDiagnosticCapture.ObserveRadius' "$patch"
grep -Fq 'if (replaced != 1)' "$patch"

if rg -n 'CalculateComfortLevel|m_comfort|m_comfortGroup|GetComfort|InShelter|m_baseTTL|m_TTLPerComfortLevel|ZNet|ZDO|RPC' "$patch"; then
  printf 'comfort range patch must not replace native comfort or Rested behavior\n' >&2
  exit 1
fi

# The one-shot command observes the real native calculation. It reuses native
# comfort query for radius exclusions and emits typed evidence without writes.
rg -Fq 'SE_Rested.CalculateComfortLevel(player)' "$capture"
rg -Fq 'Piece.GetAllComfortPiecesInRadius(position, float.MaxValue' "$capture"
rg -Fq '[HarmonyPatch(typeof(Piece), nameof(Piece.GetComfort))]' "$capture"
rg -Fq 'DiagnosticEvent.Create("Comfort", "comfort_debug_summary")' "$command"
rg -Fq 'DiagnosticEvent.Create("Comfort", "comfort_debug_piece")' "$command"
rg -Fq '.String("native_prefilter_visibility", "not_observable")' "$command"
rg -Fq '.String("identity_scope", piece.IdentityScope)' "$command"
rg -Fq 'ComfortDiagnosticSummary.Format' "$command"
rg -Fq 'lines.Add("COUNTED")' "$summary"
rg -Fq 'lines.Add("IGNORED")' "$summary"
rg -Fq 'lines.Add("JUST OUTSIDE RANGE")' "$summary"
rg -Fq '"session_only"' "$capture"
rg -Fq 'ComfortDiagnosticCommand.TryExecute(args.Args, args.Context)' "$client"
rg -Fq 'ComfortDiagnosticCommand.PrintUsage(context)' "$client"

# A failed Harmony startup removes every observation hook. The command must
# refuse capture before it can emit a plausible empty snapshot.
command_gate="$(sed -n '/internal static bool TryExecute/,/private static void Emit/p' "$command")"
grep -Fq 'if (!HealthReporting.GameplayActionsEnabled)' <<<"$command_gate"
grep -Fq 'required observation hooks did not load' <<<"$command_gate"
health_line="$(grep -n 'if (!HealthReporting.GameplayActionsEnabled)' "$command" | cut -d: -f1)"
emit_line="$(grep -n 'Emit(player, context);' "$command" | cut -d: -f1)"
if [[ "$health_line" -ge "$emit_line" ]]; then
  printf 'comfort diagnostic health gate must run before capture\n' >&2
  exit 1
fi

if rg -n 'Set\(|InvokeRPC|ZDOMan|ZRoutedRpc|ZNetScene|Destroy\(|Instantiate\(' "$capture" "$command" "$summary"; then
  printf 'comfort diagnostic must not mutate native, network, persistent, or world state\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/comfort-diagnostic/ComfortDiagnosticTests.csproj"

printf 'comfort furniture range source checks passed\n'
