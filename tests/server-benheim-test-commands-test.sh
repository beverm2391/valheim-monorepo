#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
server="$root/server-mods/benheim-test-commands/src/BoarTestCommandServer.cs"
plugin="$root/server-mods/benheim-test-commands/src/Plugin.cs"
diagnostics="$root/server-mods/benheim-test-commands/src/ServerDiagnostics.cs"
profile_patches="$root/server-mods/benheim-test-commands/src/BoarTierIdentityPatches.cs"
project="$root/server-mods/benheim-test-commands/src/BenheimTestCommands.csproj"
build_script="$root/server-mods/benheim-test-commands/scripts/build.sh"
source_tree="$($root/mods/benheim-qol/scripts/ensure-valheim-source.sh)"

grep -Fq 'PluginVersion = "0.1.0"' "$plugin"
grep -Fq 'ZNet.instance.IsServer()' "$server"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnNewConnection")]' "$server"
grep -Fq 'peer.m_rpc.Register<string, int>(' "$server"
grep -Fq 'peer.IsReady()' "$server"
grep -Fq 'ReferenceEquals(rpc, peer.m_rpc)' "$server"
grep -Fq 'ZNet.instance.IsAdmin(rpc.GetSocket().GetHostName())' "$server"
grep -Fq 'peer.m_refPos + SpawnOffset' "$server"
grep -Fq 'GameObject? prefab = scene.GetPrefab(BoarPrefabName);' "$server"
grep -Fq 'Character? character = spawned.GetComponent<Character>();' "$server"
grep -Fq 'character.SetLevel(level);' "$server"
grep -Fq 'scene.Destroy(spawned);' "$server"
grep -Fq 'TrySendResult(rpc, safeOperationId, "accepted", "spawned", level);' "$server"
grep -Fq 'boar_test_spawn_result_delivery_failed' "$server"
grep -Fq 'boar_test_spawn_requested' "$server"
grep -Fq 'boar_test_spawn_accepted' "$server"
grep -Fq 'boar_test_spawn_rejected' "$server"
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
grep -Fq '!character.IsOwner()' "$root/mods/benheim-qol/src/EnemyTiers/BoarTierCombat.cs"
grep -Fq 'ServerDiagnostics.Emit(diagnosticEvent);' "$profile_patches"
grep -Fq 'BoarTierIdentity.cs' "$project"
grep -Fq 'BoarTierCombat.cs' "$project"
grep -Fq 'BoarTierPhysicalProfile.cs' "$project"
grep -Fq 'UnityEngine.PhysicsModule' "$project"
grep -Fq 'dist/BenheimTestCommands.dll' "$build_script"
grep -Fq 'ListContainsId(m_adminList, rpc.GetSocket().GetHostName())' "$source_tree/ZNet.cs"
grep -Fq 'rpc.Register("Save", RPC_Save);' "$source_tree/ZNet.cs"
grep -Fq 'public Vector3 m_refPos = Vector3.zero;' "$source_tree/ZNetPeer.cs"

if rg -n 'devcommands|ZRoutedRpc|InvokeRoutedRPC|Teleport|SetGlobalKey|RemoveGlobalKey|KillAll|ItemDrop|Heightmap' "$server" ||
  rg -Pn 'GetPrefab\((?!BoarPrefabName\))' "$server"; then
  printf 'server test commands must remain a fixed Boar-only allowlist\n' >&2
  exit 1
fi

if rg -n 'Object\.Destroy\(' "$server"; then
  printf 'network Boar cleanup must remove the native ZDO through ZNetScene\n' >&2
  exit 1
fi

dotnet build "$project" --configuration Release
printf 'Benheim dedicated-server test-command checks passed\n'
