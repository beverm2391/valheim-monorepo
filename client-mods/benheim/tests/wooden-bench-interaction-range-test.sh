#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_tree="$("$root/scripts/ensure-valheim-source.sh")"
piece="$source_tree/Piece.cs"
chair="$source_tree/Chair.cs"

# Piece.Awake sees the serialized child seats; Chair uses m_useDistance for
# both native hover and the native sit action.
grep -Fq 'private void Awake()' "$piece"
grep -Fq 'public float m_useDistance = 2f;' "$chair"
grep -Fq 'private bool InUseDistance(Humanoid human)' "$chair"
grep -Fq 'return Vector3.Distance(human.transform.position, m_attachPoint.position) < m_useDistance;' "$chair"
grep -Fq 'if (!InUseDistance(Player.m_localPlayer))' "$chair"
grep -Fq 'if (!InUseDistance(player))' "$chair"
grep -Fq 'player.AttachStart(m_attachPoint' "$chair"
grep -Fq 'Player.GetClosestPlayer(m_attachPoint.position, 0.05f) != null' "$chair"

# A new Chair initializer or field write could undo Piece.Awake configuration.
# Force inspection when the native lifecycle changes.
if grep -Eq 'void (Awake|Start)\(' "$chair" ||
   [[ $(grep -Ec 'm_useDistance[[:space:]]*=' "$chair") != 1 ]]; then
  printf 'Native Chair range initialization changed; inspect before extending it\n' >&2
  exit 1
fi

printf 'Wooden bench interaction range native-contract checks passed\n'
