#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
chance="$root/src/EnemyTiers/WildernessStarChance.cs"
patches="$root/src/EnemyTiers/WildernessStarPatches.cs"
tuning="$root/src/EnemyTiers/BiomeStarChanceTuning.cs"
map_overlay="$root/src/EnemyTiers/WildernessMapOverlay.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_spawn="$source_tree/SpawnSystem.cs"

rg -Fq 'WorldGenerator.worldSize' "$patches"
rg -Fq 'Utils.LengthXZ(spawnPoint)' "$patches"
rg -Fq 'zoneSystem.GetGroundData(' "$patches"
rg -Fq 'eventSpawner' "$patches"
rg -Fq 'Character.InInterior(spawnPoint)' "$patches"
rg -Fq 'source=ordinary_wilderness' "$patches"
rg -Fq 'distance_multiplier=' "$patches"
rg -Fq 'TryGetCurve' "$tuning"
rg -Fq 'WorldEdgeBonus = 0.75f' "$chance"
rg -Fq 'MaxChancePercent = 30f' "$chance"
rg -Fq 'NormalizeDistance(' "$chance"
rg -Fq '___m_explored' "$map_overlay"
rg -Fq '___m_exploredOthers' "$map_overlay"
rg -Fq '___m_showSharedMapData' "$map_overlay"
rg -Fq 'overlayImage.raycastTarget = false' "$map_overlay"
rg -Fq 'WildernessDangerScale.Label(danger)' "$map_overlay"
rg -Fq 'ComposeChance(' "$map_overlay"

if rg -n 'percent|%|dungeon|raid|alpha|event' "$map_overlay"; then
  printf 'map presentation must stay qualitative and ordinary-wilderness scoped\n' >&2
  exit 1
fi

if rg -n 'CreatureSpawner|SpawnArea|RandomEvent|RandEventSystem|UpdateSpawnList' "$patches"; then
  printf 'enemy tier foundation must not patch local spawners or random events\n' >&2
  exit 1
fi

rg -Fq 'UpdateSpawnList(spawnList.m_spawners, time, eventSpawners: false)' "$native_spawn"
rg -Fq 'UpdateSpawnList(currentSpawners, time, eventSpawners: true)' "$native_spawn"
rg -Fq 'm_levelUpMinCenterDistance <= 0f || spawnPoint.magnitude > critter.m_levelUpMinCenterDistance' "$native_spawn"
rg -Fq 'm_requiredGlobalKey' "$native_spawn"
rg -Fq 'm_requiredEnvironments' "$native_spawn"
rg -Fq 'm_spawnAtDay' "$native_spawn"
rg -Fq 'm_spawnAtNight' "$native_spawn"
rg -Fq 'IsSpawnPointGood(spawner, ref spawnPoint)' "$native_spawn"
rg -Fq 'int i = critter.m_minLevel;' "$native_spawn"
rg -Fq 'for (; i < critter.m_maxLevel; i++)' "$native_spawn"

rg -Fq '[HarmonyPatch(typeof(SpawnSystem), "Awake")]' "$root/src/Spawning/LeechSpawnPatches.cs"
rg -Fq 'LeechSpawnFrequency.AdjustInterval(nativeInterval)' "$root/src/Spawning/LeechSpawnPatches.cs"

dotnet run --project "$root/tests/enemy-tier-distance/EnemyTierDistanceTests.csproj"

printf 'enemy tier distance source and behavioral checks passed\n'
