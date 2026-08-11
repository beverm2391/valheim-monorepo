#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
chance="$root/src/EnemyTiers/WildernessStarChance.cs"
patches="$root/src/EnemyTiers/WildernessStarPatches.cs"
tuning="$root/src/EnemyTiers/BiomeStarChanceTuning.cs"
map_hover="$root/src/EnemyTiers/WildernessMapHover.cs"
map_label_layout="$root/src/EnemyTiers/WildernessMapLabelLayout.cs"
danger_presentation="$root/src/EnemyTiers/WildernessDangerPresentation.cs"
danger_transition="$root/src/EnemyTiers/WildernessDangerTransition.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_spawn="$source_tree/SpawnSystem.cs"

rg -Fq 'WorldGenerator.worldSize' "$patches"
rg -Fq 'Utils.LengthXZ(spawnPoint)' "$patches"
rg -Fq 'zoneSystem.GetGroundData(' "$patches"
rg -Fq 'eventSpawner' "$patches"
rg -Fq 'Character.InInterior(spawnPoint)' "$patches"
rg -Fq 'source=ordinary_wilderness' "$patches"
rg -Fq 'global_distance_addition=' "$patches"
rg -Fq 'TryGetCurve' "$tuning"
rg -Fq 'WorldEdgeAdditionPercent = 10f' "$chance"
rg -Fq 'NormalizeDistance(' "$chance"
rg -Fq '___m_explored' "$map_hover"
rg -Fq '___m_exploredOthers' "$map_hover"
rg -Fq '___m_showSharedMapData' "$map_hover"
rg -Uq '\[HarmonyPatch\]\ninternal static class WildernessMapHover' "$map_hover"
rg -Fq 'WildernessDangerScale.StyledLabel(hovered.Danger)' "$map_hover"
rg -Fq '$"{nativeText}\n{WildernessDangerScale.StyledLabel(hovered.Danger)}"' "$map_hover"
rg -Fq 'RestoreNativeLabelBounds(label);' "$map_hover"
rg -Fq 'WildernessMapLabelLayout.ExpandDownward(' "$map_hover"
rg -Fq 'nativeAnchoredY - ((1f - pivotY) * addedHeight)' "$map_label_layout"
rg -Fq 'ComposeChance(' "$map_hover"
rg -Fq 'wilderness_map_hover' "$map_hover"
rg -Fq 'wilderness_map_hover_probe' "$map_hover"
rg -Fq '"patch_invoked"' "$map_hover"
rg -Fq '"large_map_ready"' "$map_hover"
rg -Fq '"local_point_rejected"' "$map_hover"
rg -Fq '"bounds_rejected"' "$map_hover"
rg -Fq '"exploration_hidden"' "$map_hover"
rg -Fq '"exploration_visible"' "$map_hover"
rg -Fq 'stage=unsupported_biome' "$map_hover"
rg -Fq 'stage=classified' "$map_hover"
rg -Fq 'local_explored=' "$map_hover"
rg -Fq 'shared_explored=' "$map_hover"
rg -Fq 'show_shared=' "$map_hover"
rg -Uq '\[HarmonyPatch\]\ninternal static class WildernessDangerPresentationPatches' "$danger_presentation"
rg -Fq '[HarmonyPatch(typeof(Minimap), "UpdateBiome")]' "$danger_presentation"
rg -Fq 'player.GetCurrentBiome()' "$danger_presentation"
rg -Fq 'Utils.LengthXZ(player.transform.position)' "$danger_presentation"
rg -Fq 'WildernessStarChance.ComposeChance(' "$danger_presentation"
rg -Fq 'm_biomeNameSmall.text' "$danger_presentation"
rg -Fq '<size=70%>{WildernessDangerScale.StyledLabel(danger)}</size>' "$danger_presentation"
rg -Fq 'ShowBiomeFoundMsg(' "$danger_presentation"
rg -Fq '$"Entering a {WildernessDangerScale.StyledLabel(danger)} area..."' "$danger_presentation"
rg -Fq 'm_biomeFoundStinger' "$danger_presentation"
rg -Fq 'Tracker.PauseObservation();' "$danger_presentation"
rg -Fq 'MessageHud.instance.m_biomeFoundStinger != null' "$danger_presentation"
rg -Fq 'm_damageScreen' "$danger_presentation"
rg -Fq 'wilderness_danger_state' "$danger_presentation"
rg -Fq 'wilderness_danger_arrival' "$danger_presentation"
rg -Fq 'outcome=queued' "$danger_presentation"
rg -Fq 'outcome=rejected reason=cooldown' "$danger_presentation"
rg -Fq 'outcome=rejected reason=presentation_unavailable' "$danger_presentation"
rg -Fq 'wilderness_minimap_indicator' "$danger_presentation"
rg -Fq 'outcome=rendered' "$danger_presentation"
rg -Fq 'DebounceSeconds = 2f' "$danger_transition"
rg -Fq 'HysteresisPercent = 0.75f' "$danger_transition"
rg -Fq 'ArrivalCooldownSeconds = 60f' "$danger_transition"

if rg -n 'MusicMan|EnvMan|RandEventSystem|ZNetScene|ZDO|EffectList|SetForceEnvironment' "$danger_presentation"; then
  printf 'danger presentation must not control music, weather, events, or world state\n' >&2
  exit 1
fi

if rg -n 'percent|%|dungeon|raid|alpha|event| · | wilderness"| threat"|star risk' "$map_hover"; then
  printf 'map presentation must stay qualitative and ordinary-wilderness scoped\n' >&2
  exit 1
fi

if rg -n 'Texture2D|RawImage|GeneratePressureColors|SetPixel|GetPixels|for \(' "$map_hover"; then
  printf 'map hover must not precompute or incrementally render pressure pixels\n' >&2
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
