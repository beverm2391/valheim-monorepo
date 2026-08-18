#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
client="$root/mods/benheim-qol/src/KillAttribution"
server="$root/server-mods/benheim-server-support/src/KillAttributionServer.cs"
server_patches="$root/server-mods/benheim-server-support/src/KillAttributionServerPatches.cs"
chain_state="$root/server-mods/benheim-server-support/src/KillChainState.cs"
delivery_queue="$root/server-mods/benheim-server-support/src/KillChainDeliveryQueue.cs"
delivery_attempt="$root/server-mods/benheim-server-support/src/KillChainDeliveryAttempt.cs"
delivery_runtime="$root/server-mods/benheim-server-support/src/KillChainDeliveryRuntime.cs"
qualification="$root/server-mods/benheim-server-support/src/VictimQualification.cs"
plugin="$root/server-mods/benheim-server-support/src/Plugin.cs"
project="$root/server-mods/benheim-server-support/src/BenheimServerSupport.csproj"

grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]' "$client/KillAttributionPatches.cs"
grep -Fq 'LethalHitObservation.Capture(__instance, hit)' "$client/KillAttributionPatches.cs"
grep -Fq '__state.BecameLethal(__instance)' "$client/KillAttributionPatches.cs"
grep -Fq 'victim.IsOwner()' "$client/LethalHitObservation.cs"
grep -Fq '!(hit.GetAttacker() is Player killer)' "$client/LethalHitObservation.cs"
grep -Fq 'ZNet.instance.GetServerRPC()' "$client/KillAttributionClient.cs"
grep -Fq 'KillAttributionProtocol.CapabilityRpc' "$client/KillAttributionClient.cs"
grep -Fq 'HasCompatibleServer' "$client/KillAttributionClient.cs"
grep -Fq 'ReferenceEquals(rpc, ZNet.instance?.GetServerRPC())' "$client/KillAttributionClient.cs"
grep -Fq 'PlayerCombatRuntime.Publish(' "$client/KillAttributionClient.cs"
grep -Fq 'new ConfirmedKill(' "$client/KillAttributionClient.cs"
grep -Fq 'KillAttributionProtocol.ChainTransitionRpc' "$client/KillAttributionClient.cs"
grep -Fq 'new BerserkerChainTransition(' "$client/KillAttributionClient.cs"
grep -Fq 'ChainDelivery.TryAccept(message.Kind, message.ServerSequence)' "$client/KillAttributionClient.cs"
grep -Fq '[HarmonyPatch(typeof(Player), "OnDeath")]' "$client/KillAttributionPatches.cs"
grep -Fq 'KillAttributionProtocol.ChainResetRpc' "$client/KillAttributionClient.cs"

grep -Fq 'ReferenceEquals(rpc, reporter.m_rpc)' "$server"
grep -Fq 'KillAttributionProtocol.CapabilityRpc' "$server"
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
grep -Fq 'KillAttributionProtocol.ChainTransitionRpc' "$delivery_runtime"
grep -Fq 'KillAttributionServer.Update();' "$plugin"
grep -Fq 'serverTimeSeconds < current.ExpiresAtServerTimeSeconds' "$chain_state"
grep -Fq 'WindowSeconds = 10d' "$chain_state"
grep -Fq 'BerserkerThreshold = 3' "$chain_state"
grep -Fq 'SlaughterhouseThreshold = 6' "$chain_state"
grep -Fq 'KillChainDeliveryRuntime.Update(serverTimeSeconds, Chains)' "$server"
grep -Fq 'Pending.HasPending(transition.Killer)' "$delivery_runtime"
grep -Fq 'Abandon(' "$delivery_runtime"
grep -Fq 'maximumPerKiller' "$delivery_queue"
grep -Fq 'killerPeer.m_rpc.IsConnected()' "$delivery_runtime"
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

# The first slice must remain direct-Player last-hit attribution. It must not
# grow provenance guesses for explicitly unsupported sources.
if rg -n 'SE_Poison|SE_Burning|StatusEffect|Tameable|Turret|Trap|assist|kill.?steal' "$client" "$server" "$server_patches"; then
  printf 'kill attribution must not infer unsupported provenance\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/kill-attribution/KillAttributionTests.csproj"
printf 'Benheim confirmed-kill source checks passed\n'
