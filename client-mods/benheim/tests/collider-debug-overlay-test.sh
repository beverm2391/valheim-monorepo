#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
overlay="$root/src/EnemyTiers/CharacterColliderOverlay.cs"
rg -Fq 'internal static bool TrySetActive(bool requestedState, out string failure)' "$overlay"
rg -Fq 'DiagnosticEvent.Create("EnemyTiers", "character_collider_overlay_toggled")' "$overlay"

rg -Fq 'Character.GetAllCharacters()' "$overlay"
rg -Fq 'character.GetCollider()' "$overlay"
rg -Fq 'character.IsPlayer()' "$overlay"
rg -Fq 'sqrMagnitude > MaximumDistanceSquared' "$overlay"
rg -Fq 'Collider.transform' "$overlay"
rg -Fq 'Collider.center' "$overlay"
rg -Fq 'Collider.radius' "$overlay"
rg -Fq 'Collider.height' "$overlay"
rg -Fq 'Collider.direction' "$overlay"
rg -Fq 'LineRenderer' "$overlay"
rg -Fq 'line.useWorldSpace = true' "$overlay"
rg -Fq 'CompareFunction.Always' "$overlay"

rg -Fq 'Player.m_localPlayer == null' "$overlay"
rg -Fq 'UnityEngine.Object.Destroy(root)' "$overlay"
rg -Fq 'UnityEngine.Object.Destroy(lineMaterial)' "$overlay"
rg -Fq 'UnityEngine.Object.Destroy(lineObject)' "$overlay"

if rg -q 'Physics\.|Rigidbody|ZRoutedRpc|ZDO|GetComponents|new Vector3\[|System\.Linq|bh debug' "$overlay"; then
  echo "collider overlay must remain observational, bounded, and allocation-stable" >&2
  exit 1
fi

update_body="$(sed -n '/internal static void Update()/,/internal static void Reset()/p' "$overlay")"
if grep -q 'Diagnostics\.' <<<"$update_body"; then
  echo "collider overlay update must not emit per-frame diagnostics" >&2
  exit 1
fi

echo "collider debug overlay source checks passed"
