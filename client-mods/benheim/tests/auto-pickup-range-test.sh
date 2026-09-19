#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patch="$root/src/Interaction/AutoPickupRangePatch.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_player="$source_tree/Player.cs"

# The installed game uses this field only for the native auto-pickup query and
# its final distance check. A future new use needs review before field mutation
# can still be claimed to leave manual interaction and other behavior native.
grep -Fq 'm_autoPickupRange = 2f' "$native_player"
grep -Fq 'Physics.OverlapSphereNonAlloc(vector, m_autoPickupRange' "$native_player"
grep -Fq 'if (num2 > m_autoPickupRange)' "$native_player"
test "$(rg -c 'm_autoPickupRange' "$native_player")" -eq 3
test "$(rg -l 'm_autoPickupRange' "$source_tree" --glob '*.cs' | wc -l)" -eq 1
grep -Fq 'if (IsTeleporting() || !m_enableAutoPickup)' "$native_player"
grep -Fq '!m_inventory.CanAddItem(component.m_itemData)' "$native_player"
grep -Fq '[HarmonyPatch(typeof(Player), "Awake")]' "$patch"
grep -Fq '__instance.m_autoPickupRange = extendedRange;' "$patch"

dotnet run --project "$root/tests/native-mechanic-transpilers/NativeMechanicTranspilerTests.csproj"

printf 'Auto-pickup range checks passed\n'
