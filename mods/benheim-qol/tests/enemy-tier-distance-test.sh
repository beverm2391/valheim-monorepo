#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
chance="$root/src/EnemyTiers/WildernessStarChance.cs"
patches="$root/src/EnemyTiers/WildernessStarPatches.cs"
tuning="$root/src/EnemyTiers/BiomeStarChanceTuning.cs"
map_hover="$root/src/EnemyTiers/WildernessMapHover.cs"
map_label_layout="$root/src/EnemyTiers/WildernessMapLabelLayout.cs"
danger_presentation="$root/src/EnemyTiers/WildernessDangerPresentation.cs"
danger_presentation_patches="$root/src/EnemyTiers/WildernessDangerPresentationPatches.cs"
minimap_indicator="$root/src/EnemyTiers/WildernessMinimapIndicator.cs"
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
rg -Fq 'WildernessDangerScale.MapLabel(hovered.Danger)' "$map_hover"
rg -Fq '$"{nativeText}\n{WildernessDangerScale.MapLabel(hovered.Danger)}"' "$map_hover"
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
rg -Fq '"native_biome_label_unresolved"' "$map_hover"
rg -Fq '"exploration_hidden"' "$map_hover"
rg -Fq '"exploration_visible"' "$map_hover"
rg -Fq 'stage=unsupported_biome' "$map_hover"
rg -Fq 'stage=classified' "$map_hover"
rg -Fq 'local_explored=' "$map_hover"
rg -Fq 'shared_explored=' "$map_hover"
rg -Fq 'show_shared=' "$map_hover"
rg -Uq '\[HarmonyPatch\]\ninternal static class WildernessDangerPresentationPatches' "$danger_presentation_patches"
rg -Fq '[HarmonyPatch(typeof(Minimap), "UpdateBiome")]' "$danger_presentation_patches"
rg -Fq 'player.GetCurrentBiome()' "$danger_presentation"
rg -Fq 'Utils.LengthXZ(player.transform.position)' "$danger_presentation"
rg -Fq 'WildernessStarChance.ComposeChance(' "$danger_presentation"
rg -Fq 'TMP_Text label = minimap.m_biomeNameSmall;' "$minimap_indicator"
rg -Fq '$"{nativeBiome}\n{WildernessDangerScale.MapLabel(danger)}"' "$minimap_indicator"
rg -Fq 'RestoreNativeBounds(label);' "$minimap_indicator"
rg -Fq 'label.GetPreferredValues(nativeText, width, Mathf.Infinity).y' "$minimap_indicator"
rg -Fq 'WildernessMapLabelLayout.ExpandDownward(' "$minimap_indicator"
rg -Fq 'nativeAnchoredPosition.y' "$minimap_indicator"
rg -Fq 'nativeSizeDelta.y' "$minimap_indicator"
rg -Fq 'WildernessMapLabelLayout.IsResolvedNativeBiomeText(nativeBiome)' "$minimap_indicator"
rg -Fq 'label.text = lastValidBiomeText;' "$minimap_indicator"
rg -Fq 'WildernessMapLabelLayout.IsResolvedNativeBiomeText(label.text)' "$map_hover"
rg -Fq 'WildernessMapHover.Reset();' "$danger_presentation"
rg -Fq 'measuredLabel == expandedLabel' "$minimap_indicator"
rg -Fq 'ownsComposedText = labelBoundsExpanded && measuredLabel == expandedLabel' "$map_hover"
rg -Fq 'ShowBiomeFoundMsg(' "$danger_presentation"
rg -Fq '$"Entering a {WildernessDangerScale.StyledArrivalLabel(danger)} area..."' "$danger_presentation"
rg -Fq '[HarmonyPatch(typeof(MessageHud), "UpdateBiomeFound")]' "$danger_presentation_patches"
rg -Fq 'title.textWrappingMode = TextWrappingModes.NoWrap;' "$danger_presentation"
rg -Fq 'sourceFontSize = title.enableAutoSizing ? title.fontSizeMax : title.fontSize;' "$danger_presentation"
rg -Fq 'title.enableAutoSizing = false;' "$danger_presentation"
rg -Fq 'title.maxVisibleLines = 1;' "$danger_presentation"
rg -Fq 'availableWidth / preferredWidth' "$danger_presentation"
rg -Fq 'm_biomeFoundStinger' "$danger_presentation"
rg -Fq 'Tracker.PauseObservation();' "$danger_presentation"
rg -Fq 'MessageHud.instance.m_biomeFoundStinger != null' "$danger_presentation"
rg -Fq 'BenheimFxSettings.DangerArrivalEnabled' "$danger_presentation"
rg -Fq 'Hud.instance.DamageFlash();' "$danger_presentation"
rg -Fq 'vignette=native_damage_flash' "$danger_presentation"
rg -Fq 'wilderness_danger_state' "$danger_presentation"
rg -Fq 'wilderness_danger_arrival' "$danger_presentation"
rg -Fq 'outcome=queued' "$danger_presentation"
rg -Fq 'outcome=rejected reason=cooldown' "$danger_presentation"
rg -Fq 'outcome=rejected reason=presentation_unavailable' "$danger_presentation"
rg -Fq 'wilderness_minimap_indicator' "$minimap_indicator"
rg -Fq 'outcome=rendered' "$minimap_indicator"
rg -Fq 'DebounceSeconds = 2f' "$danger_transition"
rg -Fq 'HysteresisPercent = 0.75f' "$danger_transition"
rg -Fq 'ArrivalCooldownSeconds = 60f' "$danger_transition"

if [[ -e "$root/src/EnemyTiers/WildernessMapLabelContrast.cs" ]]; then
  printf 'map labels must inherit the native TMP material instead of owning a contrast layer\n' >&2
  exit 1
fi

if rg -n 'outlineColor|outlineWidth|fontSharedMaterial|<color=|<b>' "$minimap_indicator" "$map_hover"; then
  printf 'map labels must not mutate or override native TMP styling\n' >&2
  exit 1
fi

if rg -n '<size=|size=70|m_biomeNameSmall\.fontSize|new GameObject|Instantiate' "$danger_presentation" "$minimap_indicator" "$map_hover"; then
  printf 'minimap category must reuse the full-size native TMP label without a new UI surface\n' >&2
  exit 1
fi

if rg -n 'DangerousVignetteAlpha|DeadlyVignetteAlpha|damageScreen\.color|m_damageScreen\.color' "$danger_presentation"; then
  printf 'danger arrival must reuse Hud.DamageFlash rather than hold a custom alpha\n' >&2
  exit 1
fi

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
