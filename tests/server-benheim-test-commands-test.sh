#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
boar_server="$root/server-mods/benheim-test-commands/src/BoarTestCommandServer.cs"
henge_server="$root/server-mods/benheim-test-commands/src/HengeOverlayServer.cs"
plugin="$root/server-mods/benheim-test-commands/src/Plugin.cs"
diagnostics="$root/server-mods/benheim-test-commands/src/ServerDiagnostics.cs"
profile_patches="$root/server-mods/benheim-test-commands/src/BoarTierIdentityPatches.cs"
project="$root/server-mods/benheim-test-commands/src/BenheimTestCommands.csproj"
build_script="$root/server-mods/benheim-test-commands/scripts/build.sh"
source_tree="$($root/client-mods/benheim/scripts/ensure-valheim-source.sh)"

grep -Fq 'PluginVersion = "0.1.2"' "$plugin"
grep -Fq 'ZNet.instance.IsServer()' "$boar_server"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnNewConnection")]' "$boar_server"
grep -Fq 'peer.m_rpc.Register<string, int>(' "$boar_server"
grep -Fq 'peer.IsReady()' "$boar_server"
grep -Fq 'ReferenceEquals(rpc, peer.m_rpc)' "$boar_server"
grep -Fq 'ZNet.instance.IsAdmin(rpc.GetSocket().GetHostName())' "$boar_server"
grep -Fq 'peer.m_refPos + SpawnOffset' "$boar_server"
grep -Fq 'GameObject? prefab = scene.GetPrefab(BoarPrefabName);' "$boar_server"
grep -Fq 'Character? character = spawned.GetComponent<Character>();' "$boar_server"
grep -Fq 'character.SetLevel(level);' "$boar_server"
grep -Fq 'scene.Destroy(spawned);' "$boar_server"
grep -Fq 'TrySendResult(rpc, safeOperationId, "accepted", "spawned", level);' "$boar_server"
grep -Fq 'boar_test_spawn_result_delivery_failed' "$boar_server"
grep -Fq 'boar_test_spawn_requested' "$boar_server"
grep -Fq 'boar_test_spawn_accepted' "$boar_server"
grep -Fq 'boar_test_spawn_rejected' "$boar_server"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnNewConnection")]' "$henge_server"
grep -Fq 'peer.m_rpc.Register<string>(' "$henge_server"
grep -Fq 'ReferenceEquals(rpc, peer.m_rpc)' "$henge_server"
grep -Fq 'peer.IsReady()' "$henge_server"
grep -Fq 'ZNet.instance.IsAdmin(rpc.GetSocket().GetHostName())' "$henge_server"
grep -Fq 'if (!zoneSystem.LocationsGenerated)' "$henge_server"
grep -Fq 'zoneSystem.GetLocationList()' "$henge_server"
grep -Fq 'location.m_location.m_prefabName' "$henge_server"
grep -Fq 'HengeOverlayProtocol.IsHengeLocation' "$henge_server"
grep -Fq 'coordinates.Add(location.m_position);' "$henge_server"
grep -Fq 'payload.Write(coordinate);' "$henge_server"
grep -Fq 'henge_overlay_accepted' "$henge_server"
grep -Fq 'henge_overlay_rejected' "$henge_server"
grep -Fq 'HengeOverlayProtocol.cs' "$project"
grep -Fq 'DiagnosticEvent' "$diagnostics"
grep -Fq 'BenheimTestCommandEvents.ndjson' "$diagnostics"
grep -Fq '[HarmonyPatch(typeof(LevelEffects), "Start")]' "$profile_patches"
grep -Fq '[HarmonyPatch(typeof(LevelEffects), "OnLevelSet")]' "$profile_patches"
grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.ApplyPushback), typeof(Vector3), typeof(float))]' "$profile_patches"
grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.Damage), typeof(HitData))]' "$profile_patches"
grep -Fq '[HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI))]' "$profile_patches"
grep -Fq 'BoarTierIdentity.Apply' "$profile_patches"
grep -Fq 'BoarTierCombat.AdjustIncomingPush' "$profile_patches"
grep -Fq 'BoarTierCombat.AdjustOutgoingPush' "$profile_patches"
grep -Fq 'BoarTierCombat.ExtendPursuit' "$profile_patches"
grep -Fq '!character.IsOwner()' "$root/client-mods/benheim/src/EnemyTiers/BoarTierCombat.cs"
grep -Fq 'ServerDiagnostics.Emit(diagnosticEvent);' "$profile_patches"
grep -Fq 'BoarTierIdentity.cs' "$project"
grep -Fq 'BoarTierCombat.cs' "$project"
grep -Fq 'BoarTierPhysicalProfile.cs' "$project"
grep -Fq 'UnityEngine.PhysicsModule' "$project"
grep -Fq 'dist/BenheimTestCommands.dll' "$build_script"
grep -Fq 'ListContainsId(m_adminList, rpc.GetSocket().GetHostName())' "$source_tree/ZNet.cs"
grep -Fq 'rpc.Register("Save", RPC_Save);' "$source_tree/ZNet.cs"
grep -Fq 'public Vector3 m_refPos = Vector3.zero;' "$source_tree/ZNetPeer.cs"

if rg -n 'devcommands|ZRoutedRpc|InvokeRoutedRPC|Teleport|SetGlobalKey|RemoveGlobalKey|KillAll|ItemDrop|Heightmap' "$boar_server" "$henge_server" ||
  rg -Pn 'GetPrefab\((?!BoarPrefabName\))' "$boar_server" "$henge_server"; then
  printf 'server test commands must remain a fixed native-operation allowlist\n' >&2
  exit 1
fi

if rg -n 'Object\.Destroy\(' "$boar_server"; then
  printf 'network Boar cleanup must remove the native ZDO through ZNetScene\n' >&2
  exit 1
fi

if rg -n 'm_placed|GenerateLocations|CreateLocalZones|CreateGhostZones|\.Load\(|Instantiate\(|ZDO|DiscoverLocation|Explore' "$henge_server"; then
  printf 'henge overlay must only read the ready native location plan\n' >&2
  exit 1
fi

dotnet build "$project" --configuration Release
printf 'Benheim dedicated-server test-command checks passed\n'
