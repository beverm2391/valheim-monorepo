#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
client="$root/mods/benheim-qol/src/KillAttribution"
server="$root/server-mods/benheim-server-support/src/KillAttributionServer.cs"
server_patches="$root/server-mods/benheim-server-support/src/KillAttributionServerPatches.cs"
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

grep -Fq 'ReferenceEquals(rpc, reporter.m_rpc)' "$server"
grep -Fq 'KillAttributionProtocol.CapabilityRpc' "$server"
grep -Fq 'victim.GetOwner() != reporter.m_uid' "$server"
grep -Fq 'victim.GetLong(ZDOVars.s_playerID, 0L) != 0L' "$server"
grep -Fq 'peer.m_characterID == characterId' "$server"
grep -Fq 'State.TryConfirm(report.VictimId, report.KillerId, out long sequence)' "$server"
grep -Fq 'State.ReleaseFailedDelivery(report.VictimId)' "$server"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnDestroy")]' "$server"
grep -Fq '[HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]' "$server_patches"
grep -Fq 'KillAttributionProtocol.cs' "$project"
grep -Fq 'LethalHitObservation.cs' "$project"

# The first slice must remain direct-Player last-hit attribution. It must not
# grow provenance guesses for explicitly unsupported sources.
if rg -n 'SE_Poison|SE_Burning|StatusEffect|Tameable|Turret|Trap|assist|kill.?steal' "$client" "$server" "$server_patches"; then
  printf 'kill attribution must not infer unsupported provenance\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/kill-attribution/KillAttributionTests.csproj"
printf 'Benheim confirmed-kill source checks passed\n'
