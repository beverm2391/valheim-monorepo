#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
overlay="$root/src/EnemyTiers/CharacterColliderOverlay.cs"
client="$root/src/EnemyTiers/BenheimTestCommandClient.cs"

rg -Fq 'arguments[1], "debug"' "$overlay"
rg -Fq 'arguments[2], "colliders"' "$overlay"
rg -Fq 'arguments[3], "on"' "$overlay"
rg -Fq 'arguments[3], "off"' "$overlay"
rg -Fq 'CharacterColliderOverlay.TryExecute(args.Args, args.Context)' "$client"
rg -Fq 'bh debug colliders on|off' "$client"

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

rg -Fq 'CharacterColliderOverlay.Update();' "$client"
rg -Fq 'CharacterColliderOverlay.Reset();' "$client"
rg -Fq 'Player.m_localPlayer == null' "$overlay"
rg -Fq 'UnityEngine.Object.Destroy(root)' "$overlay"
rg -Fq 'UnityEngine.Object.Destroy(lineMaterial)' "$overlay"

if rg -q 'Physics\.|Rigidbody|ZRoutedRpc|ZDO|GetComponents|new Vector3\[|System\.Linq' "$overlay"; then
  echo "collider overlay must remain observational, bounded, and allocation-stable" >&2
  exit 1
fi

update_body="$(sed -n '/internal static void Update()/,/internal static void Reset()/p' "$overlay")"
if grep -q 'Diagnostics\.' <<<"$update_body"; then
  echo "collider overlay update must not emit per-frame diagnostics" >&2
  exit 1
fi

echo "collider debug overlay source checks passed"
