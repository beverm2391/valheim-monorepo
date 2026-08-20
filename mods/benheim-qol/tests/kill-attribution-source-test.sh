#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
client="$root/mods/benheim-qol/src/KillAttribution"
server="$root/server-mods/benheim-server-support/src/KillAttributionServer.cs"
server_patches="$root/server-mods/benheim-server-support/src/KillAttributionServerPatches.cs"
chain_state="$root/server-mods/benheim-server-support/src/KillChainState.cs"
chain_rules="$client/KillChainRules.cs"
delivery_attempt="$client/KillAttributionRpcAttempt.cs"
qualification="$root/server-mods/benheim-server-support/src/VictimQualification.cs"
plugin="$root/server-mods/benheim-server-support/src/Plugin.cs"
project="$root/server-mods/benheim-server-support/src/BenheimServerSupport.csproj"
protocol="$client/KillAttributionProtocol.cs"

grep -Fq 'Version = 3' "$protocol"
grep -Fq 'CapabilityRequestRpc = "Benheim_Kill_Capability_Request_V3"' "$protocol"
grep -Fq 'ChainTransitionRpc = "Benheim_Kill_Chain_Transition_V3"' "$protocol"
if rg -n '_V2' "$protocol"; then
  printf 'the 6/12/30 chain contract must not reuse Kill Attribution V2 RPC names\n' >&2
  exit 1
fi

grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]' "$client/KillAttributionPatches.cs"
grep -Fq 'LethalHitObservation.Capture(__instance, hit)' "$client/KillAttributionPatches.cs"
grep -Fq '__state.BecameLethal(__instance)' "$client/KillAttributionPatches.cs"
grep -Fq 'victim.IsOwner()' "$client/LethalHitObservation.cs"
grep -Fq '!(hit.GetAttacker() is Player killer)' "$client/LethalHitObservation.cs"
grep -Fq 'ZNet.instance.GetServerRPC()' "$client/KillAttributionClient.cs"
grep -Fq 'KillAttributionProtocol.CapabilityRpc' "$client/KillAttributionClient.cs"
grep -Fq 'KillAttributionProtocol.CapabilityRequestRpc' "$client/KillAttributionClient.cs"
grep -Fq 'CapabilityRetry.TryBeginAttempt' "$client/KillAttributionClient.cs"
grep -Fq '"current_server_rpc_established"' "$client/KillAttributionClient.cs"
grep -Fq '"capability_timeout"' "$client/KillAttributionClient.cs"
grep -Fq 'HasCompatibleServer' "$client/KillAttributionClient.cs"
grep -Fq 'ReferenceEquals(rpc, ZNet.instance?.GetServerRPC())' "$client/KillAttributionClient.cs"
grep -Fq 'PlayerCombatRuntime.Publish(' "$client/KillAttributionClient.cs"
grep -Fq 'new ConfirmedKill(' "$client/KillAttributionClient.cs"
grep -Fq 'KillAttributionProtocol.ChainTransitionRpc' "$client/KillAttributionClient.cs"
grep -Fq 'new BerserkerChainTransition(' "$client/KillAttributionClient.cs"
grep -Fq 'ChainDelivery.TryAccept(message.Kind, message.ServerSequence)' "$client/KillAttributionClient.cs"
grep -Fq 'serverRpc.IsConnected()' "$client/KillAttributionClient.cs"
grep -Fq 'HealthReporting.ReportKillAttributionUnavailable(' "$client/KillAttributionClient.cs"
grep -Fq 'KillAttributionProtocol.ChainResetAcknowledgedRpc' "$client/KillAttributionClient.cs"
grep -Fq 'if (deathResetPending)' "$client/KillAttributionClient.cs"
grep -Fq '[HarmonyPatch(typeof(Player), "OnDeath")]' "$client/KillAttributionPatches.cs"
grep -Fq 'KillAttributionProtocol.ChainResetRpc' "$client/KillAttributionClient.cs"

grep -Fq 'ReferenceEquals(rpc, reporter.m_rpc)' "$server"
grep -Fq 'KillAttributionProtocol.CapabilityRpc' "$server"
grep -Fq 'KillAttributionProtocol.CapabilityRequestRpc' "$server"
grep -Fq '"client_request_handler"' "$server"
grep -Fq '"incompatible_protocol"' "$server"
grep -Fq 'victim.GetOwner() != reporter.m_uid' "$server"
grep -Fq 'victim.GetLong(ZDOVars.s_playerID, 0L) != 0L' "$server"
grep -Fq 'peer.m_characterID == characterId' "$server"
grep -Fq 'State.TryConfirm(report.VictimId, report.KillerId, out long sequence)' "$server"
grep -Fq 'State.ReleaseFailedDelivery(report.VictimId)' "$server"
grep -Fq 'VictimQualification.IsHostileCreature(' "$server"
grep -Fq 'prefab.GetComponent<MonsterAI>() != null' "$server"
grep -Fq '"Boar".GetStableHashCode()' "$server"
grep -Fq 'Chains.Advance(' "$server"
grep -Fq 'Chains.CollectExpired(serverTimeSeconds, ExpiredChains)' "$server"
grep -Fq 'KillAttributionProtocol.ChainTransitionRpc' "$server"
grep -Fq 'KillAttributionProtocol.ChainResetAcknowledgedRpc' "$server"
grep -Fq 'Chains.RemoveKiller(reporter.m_characterID)' "$server"
grep -Fq 'KillAttributionServer.Update();' "$plugin"
grep -Fq 'serverTimeSeconds < current.ExpiresAtServerTimeSeconds' "$chain_state"
grep -Fq 'WindowSeconds = 30d' "$chain_rules"
grep -Fq 'BerserkerKillThreshold = 6' "$chain_rules"
grep -Fq 'SlaughterhouseKillThreshold = 12' "$chain_rules"
grep -Fq 'KillChainRules.WindowSeconds' "$chain_state"
grep -Fq 'killerPeer.m_rpc.IsConnected()' "$server"
grep -Fq 'if (!isConnected)' "$delivery_attempt"
if grep -Fq 'faction == Character.Faction.AnimalsVeg' "$qualification"; then
  printf 'passive AnimalsVeg must be excluded, not included in the hostile allowlist\n' >&2
  exit 1
fi
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnDestroy")]' "$server"
grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]' "$server_patches"
grep -Fq 'KillAttributionProtocol.cs' "$project"
grep -Fq 'LethalHitObservation.cs' "$project"

character_source="$($root/mods/benheim-qol/scripts/decompile-valheim.sh Character)"
grep -Fq 'public bool IsMonsterFaction(float time)' <<<"$character_source"
grep -Fq 'm_faction != Faction.ForestMonsters' <<<"$character_source"
grep -Fq 'return m_faction == Faction.MistlandsMonsters;' <<<"$character_source"

# Capability discovery retries the request because handler registration and
# ZNet's current-server identity become ready at different times. Once an RPC
# invocation begins, Benheim relies on Valheim's connection-scoped reliable
# transport. Steam queues reliable messages, PlayFab retains acknowledged
# in-flight messages, and ZNet destroys disconnected peers instead of
# reconnecting an existing ZRpc.
zrpc_source="$($root/mods/benheim-qol/scripts/decompile-valheim.sh ZRpc)"
grep -Fq 'if (IsConnected())' <<<"$zrpc_source"
grep -Fq 'SendPackage(m_pkg);' <<<"$zrpc_source"
steam_socket_source="$($root/mods/benheim-qol/scripts/decompile-valheim.sh ZSteamSocket)"
grep -Fq 'm_sendQueue.Enqueue(array);' <<<"$steam_socket_source"
grep -Fq 'SendMessageToConnection(m_con, intPtr, (uint)array.Length, 8,' <<<"$steam_socket_source"
playfab_socket_source="$($root/mods/benheim-qol/scripts/decompile-valheim.sh ZPlayFabSocket)"
grep -Fq 'm_inFlightQueue.Enqueue(array);' <<<"$playfab_socket_source"
grep -Fq 'CheckRetransmit();' <<<"$playfab_socket_source"
znet_source="$($root/mods/benheim-qol/scripts/decompile-valheim.sh ZNet)"
grep -Fq 'if (peer.m_rpc.IsConnected())' <<<"$znet_source"
grep -Fq 'Disconnect(peer);' <<<"$znet_source"

# The first slice must remain direct-Player last-hit attribution. It must not
# grow provenance guesses for explicitly unsupported sources.
if rg -n 'SE_Poison|SE_Burning|StatusEffect|Tameable|Turret|Trap|assist|kill.?steal' "$client" "$server" "$server_patches"; then
  printf 'kill attribution must not infer unsupported provenance\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/kill-attribution/KillAttributionTests.csproj"
printf 'Benheim confirmed-kill source checks passed\n'
