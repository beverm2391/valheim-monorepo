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
player_area="$root/src/EnemyTiers/WildernessPlayerArea.cs"
boar_profile="$root/src/EnemyTiers/BoarTierPhysicalProfile.cs"
boar_identity="$root/src/EnemyTiers/BoarTierIdentity.cs"
boar_patches="$root/src/EnemyTiers/BoarTierIdentityPatches.cs"
boar_combat="$root/src/EnemyTiers/BoarTierCombat.cs"
boar_command_client="$root/src/EnemyTiers/BenheimTestCommandClient.cs"
boar_command_protocol="$root/src/EnemyTiers/BoarTestCommandProtocol.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_spawn="$source_tree/SpawnSystem.cs"
native_level_effects="$source_tree/LevelEffects.cs"
native_character="$source_tree/Character.cs"
native_base_ai="$source_tree/BaseAI.cs"
native_monster_ai="$source_tree/MonsterAI.cs"
native_attack="$source_tree/Attack.cs"
native_pathfinding="$source_tree/Pathfinding.cs"
native_procreation="$source_tree/Procreation.cs"
native_growup="$source_tree/Growup.cs"

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
rg -Fq 'WildernessDangerPresentation.RefreshMinimap(__instance);' "$danger_presentation_patches"
rg -Fq 'player.GetCurrentBiome()' "$danger_presentation"
rg -Fq 'Utils.LengthXZ(player.transform.position)' "$danger_presentation"
rg -Fq 'WildernessStarChance.ComposeChance(' "$danger_presentation"
rg -Fq 'WildernessPlayerArea area = WildernessPlayerArea.Tuned(' "$danger_presentation"
rg -Fq 'PublishArea(area);' "$danger_presentation"
rg -Fq 'WildernessMinimapIndicator.Update(minimap, currentArea);' "$danger_presentation"
rg -Fq 'WildernessDangerScale.Classify(adjustedChance)' "$player_area"
rg -Fq 'TMP_Text label = minimap.m_biomeNameSmall;' "$minimap_indicator"
rg -Fq 'WildernessPlayerArea? currentArea' "$minimap_indicator"
rg -Fq 'label.text = nativeBiome;' "$minimap_indicator"
rg -Fq 'new("BenheimWildernessCategory", typeof(RectTransform))' "$minimap_indicator"
rg -Fq 'categoryRect.SetParent(nativeLabel.rectTransform, worldPositionStays: false);' "$minimap_indicator"
rg -Fq 'WildernessDangerScale.MinimapLabel(danger)' "$minimap_indicator"
rg -Fq 'categoryRect.anchorMin = new Vector2(0f, 0f);' "$minimap_indicator"
rg -Fq 'categoryRect.anchorMax = new Vector2(1f, 0f);' "$minimap_indicator"
rg -Fq 'categoryRect.pivot = new Vector2(0.5f, 1f);' "$minimap_indicator"
rg -Fq 'categoryRect.anchoredPosition = Vector2.zero;' "$minimap_indicator"
rg -Fq 'categoryRect.sizeDelta = new Vector2(0f, nativeRect.rect.height);' "$minimap_indicator"
rg -Fq 'destination.alignment = source.alignment;' "$minimap_indicator"
rg -Fq 'destination.margin = source.margin;' "$minimap_indicator"
rg -Fq 'destination.fontSharedMaterial = source.fontSharedMaterial;' "$minimap_indicator"
rg -Fq 'Object.Destroy(categoryLabel.gameObject);' "$minimap_indicator"
rg -Fq 'WildernessMapLabelLayout.IsResolvedNativeBiomeText(nativeBiome)' "$minimap_indicator"
rg -Fq 'label.text = lastValidBiomeText;' "$minimap_indicator"
rg -Fq 'WildernessMapLabelLayout.IsResolvedNativeBiomeText(label.text)' "$map_hover"
rg -Fq 'WildernessMapHover.Reset();' "$danger_presentation"
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
rg -Fq 'stage=portal_destination_resolved' "$danger_presentation"
rg -Fq 'map_danger={area.Danger}' "$danger_presentation"
rg -Fq 'arrival_danger={transition.CurrentDanger}' "$danger_presentation"
rg -Fq 'awaitingPortalDestination = false;' "$danger_presentation"
rg -Fq 'arrival_stability=' "$danger_presentation"
rg -Fq 'outcome=rejected reason=presentation_unavailable' "$danger_presentation"
rg -Fq 'wilderness_minimap_indicator' "$minimap_indicator"
rg -Fq 'outcome=rendered' "$minimap_indicator"
rg -Fq 'source=resolved_player_area' "$minimap_indicator"
rg -Fq 'distance_ratio=' "$minimap_indicator"
rg -Fq 'adjusted_chance=' "$minimap_indicator"
rg -Fq 'DebounceSeconds = 2f' "$danger_transition"
rg -Fq 'HysteresisPercent = 0.75f' "$danger_transition"
rg -Fq 'ArrivalCooldownSeconds = 60f' "$danger_transition"

rg -Fq 'case 2:' "$boar_profile"
rg -Fq 'scale: 1.4f' "$boar_profile"
rg -Fq 'case 3:' "$boar_profile"
rg -Fq 'scale: 1.7f' "$boar_profile"
rg -Fq 'incomingPushMultiplier: 0.75f' "$boar_profile"
rg -Fq 'incomingPushMultiplier: 0.55f' "$boar_profile"
rg -Fq 'outgoingPushMultiplier: 1.25f' "$boar_profile"
rg -Fq 'outgoingPushMultiplier: 1.5f' "$boar_profile"
rg -Fq 'detectionMultiplier: 1.2f' "$boar_profile"
rg -Fq 'detectionMultiplier: 1.4f' "$boar_profile"
rg -Fq 'runSpeedMultiplier: 1.08f' "$boar_profile"
rg -Fq 'runSpeedMultiplier: 1.15f' "$boar_profile"
rg -Fq 'runTurnSpeedMultiplier: 0.85f' "$boar_profile"
rg -Fq 'runTurnSpeedMultiplier: 0.7f' "$boar_profile"
rg -Fq 'pursuitDurationMultiplier: 1.25f' "$boar_profile"
rg -Fq 'pursuitDurationMultiplier: 1.5f' "$boar_profile"
rg -Uq '\[HarmonyPatch\]\ninternal static class BoarTierIdentityPatches' "$boar_patches"
rg -Fq '[HarmonyPatch(typeof(LevelEffects), "Start")]' "$boar_patches"
rg -Fq '[HarmonyPatch(typeof(LevelEffects), "OnLevelSet")]' "$boar_patches"
rg -Fq 'Utils.GetPrefabName(character.gameObject) != BoarPrefabName' "$boar_identity"
rg -Fq 'levelEffects.transform.localScale = Vector3.one * profile.VisualScale;' "$boar_identity"
rg -Fq 'collider.center = new Vector3(0f, profile.ColliderCenterY, 0f);' "$boar_identity"
rg -Fq 'collider.radius = profile.ColliderRadius;' "$boar_identity"
rg -Fq 'collider.height = profile.ColliderHeight;' "$boar_identity"
rg -Fq 'ai.m_pathAgentType = Pathfinding.AgentType.HorseSize;' "$boar_identity"
rg -Fq 'DiagnosticEvent.Create("EnemyTiers", "boar_tier_profile_applied")' "$boar_identity"
rg -Fq 'DiagnosticEvent.Create("EnemyTiers", "boar_tier_profile_rejected")' "$boar_identity"
rg -Fq 'ConditionalWeakTable<LevelEffects, BoarTierApplicationState>' "$boar_identity"
rg -Fq 'RestoreNativeProfileIfNeeded(levelEffects, character, level, source);' "$boar_identity"
rg -Fq 'DiagnosticEvent.Create("EnemyTiers", "boar_tier_profile_restored")' "$boar_identity"
rg -Fq 'ai.m_pathAgentType = Pathfinding.AgentType.Humanoid;' "$boar_identity"
rg -Fq 'state.CaptureBaseline(' "$boar_identity"
rg -Fq 'character.m_runSpeed = state.NativeRunSpeed * profile.RunSpeedMultiplier;' "$boar_identity"
rg -Fq 'character.m_runTurnSpeed = state.NativeRunTurnSpeed * profile.RunTurnSpeedMultiplier;' "$boar_identity"
rg -Fq 'ai.m_viewRange = state.NativeViewRange * profile.DetectionMultiplier;' "$boar_identity"
rg -Fq 'ai.m_hearRange = state.NativeHearRange * profile.DetectionMultiplier;' "$boar_identity"
rg -Fq 'monsterAI.m_alertRange = state.NativeAlertRange * profile.AlertRangeMultiplier;' "$boar_identity"
rg -Fq 'monsterAI.m_fleeIfNotAlerted = false;' "$boar_identity"
rg -Fq 'monsterAI.m_fleeIfNotAlerted = state.NativeFleeIfNotAlerted;' "$boar_identity"
rg -Fq 'incoming_push_multiplier' "$boar_identity"
rg -Fq 'pursuit_duration_multiplier' "$boar_identity"
rg -Fq '[HarmonyPatch(typeof(Character), nameof(Character.ApplyPushback), typeof(Vector3), typeof(float))]' "$boar_patches"
rg -Fq '[HarmonyPatch(typeof(Character), nameof(Character.Damage), typeof(HitData))]' "$boar_patches"
rg -Fq '[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]' "$boar_patches"
rg -Fq 'BoarTierCombat.AdjustIncomingPush' "$boar_patches"
rg -Fq 'BoarTierCombat.AdjustOutgoingPush' "$boar_patches"
rg -Fq 'BoarTierCombat.ExtendPursuit' "$boar_patches"
rg -Fq 'pushForce *= profile.IncomingPushMultiplier;' "$boar_combat"
rg -Fq 'hit.m_pushForce *= profile.OutgoingPushMultiplier;' "$boar_combat"
rg -Fq 'if (!target.IsPlayer())' "$boar_combat"
rg -Fq '!character.IsOwner()' "$boar_combat"
rg -Fq 'arguments.Length == 4' "$boar_command_protocol"
rg -Fq 'IsHelpRequest' "$boar_command_protocol"
rg -Fq 'TryParseSpawnBoar' "$boar_command_protocol"
rg -Fq 'string.Equals(arguments[1], "spawn", StringComparison.OrdinalIgnoreCase)' "$boar_command_protocol"
rg -Fq 'string.Equals(arguments[2], "boar", StringComparison.OrdinalIgnoreCase)' "$boar_command_protocol"
rg -Fq '(stars == 0 || stars == 1 || stars == 2)' "$boar_command_protocol"
rg -Fq 'case 0:' "$boar_command_protocol"
rg -Fq 'level = 1;' "$boar_command_protocol"
rg -Fq 'case 1:' "$boar_command_protocol"
rg -Fq 'level = 2;' "$boar_command_protocol"
rg -Fq 'case 2:' "$boar_command_protocol"
rg -Fq 'level = 3;' "$boar_command_protocol"
rg -Fq 'new Terminal.ConsoleCommand(' "$boar_command_client"
rg -Fq '"bh"' "$boar_command_client"
rg -Fq 'BoarTestCommandProtocol.IsHelpRequest(args.Args)' "$boar_command_client"
rg -Fq 'BoarTestCommandProtocol.TryParseSpawnBoar(args.Args' "$boar_command_client"
rg -Fq 'PrintHelp(args.Context);' "$boar_command_client"
rg -Fq 'isCheat: false' "$boar_command_client"
rg -Fq 'serverRpc.Invoke(BoarTestCommandProtocol.RequestRpc, operationId, stars);' "$boar_command_client"
rg -Fq 'ReferenceEquals(rpc, ZNet.instance?.GetServerRPC())' "$boar_command_client"
rg -Fq 'boar_test_spawn_requested' "$boar_command_client"
rg -Fq 'boar_test_spawn_result' "$boar_command_client"

if [[ -e "$root/src/EnemyTiers/BoarTestCommandClient.cs" ]]; then
  printf 'retired single-creature command owner must not remain after bh command migration\n' >&2
  exit 1
fi
rg -Fq 'PendingOperations[operationId] = Time.realtimeSinceStartup;' "$boar_command_client"
rg -Fq 'PendingOperations.Remove(operationId)' "$boar_command_client"
rg -Fq 'ExpireUnansweredRequests(Time.realtimeSinceStartup);' "$boar_command_client"
rg -Fq '"server_no_response"' "$boar_command_client"

if rg -n 'devcommands|onlyAdmin: true|ZRoutedRpc|InvokeRoutedRPC|GetPrefab\(|Object\.Instantiate|SetLevel\(' "$boar_command_client" "$boar_command_protocol"; then
  printf 'client test command must only request fixed server-authoritative operations\n' >&2
  exit 1
fi
rg -Fq 'm_level = m_nview.GetZDO().GetInt(ZDOVars.s_level, 1);' "$native_character"
rg -Fq 'm_onLevelSet(m_level);' "$native_character"
rg -Fq 'SetupLevelVisualization(m_character.GetLevel());' "$native_level_effects"
rg -Fq 'new Action<int>(OnLevelSet)' "$native_level_effects"
rg -Fq 'agentSettings9.m_build.agentHeight = 2.5f;' "$native_pathfinding"
rg -Fq 'agentSettings9.m_build.agentRadius = 0.8f;' "$native_pathfinding"
rg -Fq 'component.SetLevel(Mathf.Max(m_minOffspringLevel, m_character ? m_character.GetLevel() : m_minOffspringLevel));' "$native_procreation"
rg -Fq 'component2.SetLevel(component.GetLevel());' "$native_growup"
rg -Fq 'float num = pushForce * Mathf.Clamp01(1f + GetEquipmentMovementModifier()) / m_body.mass * 2.5f;' "$native_character"
rg -Fq 'm_nview.InvokeRPC("RPC_Damage", hit);' "$native_character"
rg -Fq 'hitData.m_pushForce = m_weapon.m_shared.m_attackForce * skillDamageFactor * m_forceMultiplier;' "$native_attack"
rg -Fq 'AddStaggerDamage(totalStaggerDamage * hit.m_staggerMultiplier, hit.m_dir, hit);' "$native_character"
rg -Fq 'public float m_viewRange = 50f;' "$native_base_ai"
rg -Fq 'public float m_hearRange = 9999f;' "$native_base_ai"
rg -Fq 'public float m_alertRange = 9999f;' "$native_monster_ai"
rg -Fq 'm_timeSinceSensedTargetCreature > 30f' "$native_monster_ai"

if rg -n 'transform\.localScale \*=|m_speed|m_walkSpeed|m_mass|\.mass =|m_swim|m_damageModifiers|m_attackForce|m_attackSpeed|m_minAttackInterval|m_staggerDamageFactor|m_staggerMultiplier' "$boar_profile" "$boar_identity" "$boar_patches" "$boar_combat"; then
  printf 'Boar tier experiment must not change damage, resistance, attack cadence, animation speed, mass, swim, or stagger tuning\n' >&2
  exit 1
fi

if rg -n 'GetZDO\(\).*Set|ZDOVars|ZNetScene|Object\.Instantiate|Clone|m_shared' "$boar_profile" "$boar_identity" "$boar_patches" "$boar_combat"; then
  printf 'Boar tier identity must not add persistent state or custom prefab machinery\n' >&2
  exit 1
fi

if rg -n 'Player\.m_localPlayer|GetCurrentBiome\(' "$minimap_indicator"; then
  printf 'minimap must consume one resolved player-area sample instead of rereading the player\n' >&2
  exit 1
fi

if rg -n 'WildernessDangerPresentation\.CurrentDanger|currentDanger =|PortalDisplay' "$danger_presentation" "$danger_presentation_patches"; then
  printf 'minimap must not consume the arrival tracker as its factual category owner\n' >&2
  exit 1
fi

if [[ -e "$root/src/EnemyTiers/WildernessMapLabelContrast.cs" ]]; then
  printf 'map labels must inherit the native TMP material instead of owning a contrast layer\n' >&2
  exit 1
fi

if rg -n 'outlineColor|outlineWidth|<color=|<b>' "$minimap_indicator" "$map_hover"; then
  printf 'map labels must not mutate or override native TMP styling\n' >&2
  exit 1
fi

if rg -n '<size=|size=70|m_biomeNameSmall\.fontSize|Object\.Instantiate|\$"\{nativeBiome\}\\n' "$danger_presentation" "$minimap_indicator" "$map_hover"; then
  printf 'minimap category must use one separate full-size native-styled line without markup or combined text\n' >&2
  exit 1
fi

if rg -n 'ForceMeshUpdate|textBounds|TextAlignmentOptions\.Center|category_center_offset' "$minimap_indicator"; then
  printf 'minimap category must follow the native right edge without centered glyph geometry\n' >&2
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
