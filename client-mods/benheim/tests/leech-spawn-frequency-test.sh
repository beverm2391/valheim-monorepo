#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
frequency="$root/src/Spawning/LeechSpawnFrequency.cs"
patches="$root/src/Spawning/LeechSpawnPatches.cs"

grep -Fq 'PrefabName = "Leech"' "$frequency"
grep -Fq 'OpportunityMultiplier = 3f' "$frequency"
grep -Fq 'return nativeInterval / OpportunityMultiplier;' "$frequency"
grep -Fq 'ConditionalWeakTable<T, Marker>' "$frequency"
grep -Fq '[HarmonyPatch(typeof(SpawnSystem), "Awake")]' "$patches"
grep -Fq '[HarmonyPatch(typeof(ZNetScene), "Awake")]' "$patches"
grep -Fq 'PendingSpawnSystems' "$patches"
grep -Fq 'leech_interval_failed' "$patches"
grep -Fq 'spawner.m_prefab != prefab' "$patches"
grep -Fq 'LeechSpawnFrequency.AdjustInterval(nativeInterval)' "$patches"
grep -Fq 'source=base_world' "$patches"
grep -Fq 'Diagnostics.Event(' "$patches"

if rg -n 'RandEventSystem|UpdateSpawning|ZDO|RPC|ZoneSystem' "$frequency" "$patches"; then
  printf 'leech frequency patch must not alter events, ownership, RPCs, or native spawn execution\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/leech-spawn-frequency/LeechSpawnFrequencyTests.csproj"

printf 'leech spawn frequency source and behavioral checks passed\n'
