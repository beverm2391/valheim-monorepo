#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mod="$root/server-mods/benheim-server-support"
server="$mod/src/PutAwayLeaseServer.cs"
state="$mod/src/PutAwayLeaseState.cs"
plugin="$mod/src/Plugin.cs"
project="$mod/src/BenheimServerSupport.csproj"
client="$root/mods/benheim-qol/src/Inventory/PutAwayLeaseClient.cs"
quick_stack="$root/mods/benheim-qol/src/Inventory/QuickStack.cs"

grep -Fq 'PluginName = "Benheim Server Support"' "$plugin"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnNewConnection")]' "$server"
grep -Fq '[HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect), typeof(ZNetPeer))]' "$server"
grep -Fq 'peer.m_rpc.Register<string>(' "$server"
grep -Fq 'ReferenceEquals(rpc, peer.m_rpc)' "$server"
grep -Fq 'Lease.TryAcquire(peer, safeOperationId)' "$server"
grep -Fq 'Lease.TryReleasePeer(peer, out string operationId)' "$server"
grep -Fq 'lock (sync)' "$state"
grep -Fq 'result.Reason == "busy"' "$quick_stack"
grep -Fq 'Put Away busy — retry in a few seconds' "$quick_stack"
grep -Fq 'Put Away timed out — reconnect to safely retry this chest' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.TryRequest' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.Release("native_response_timeout")' "$quick_stack"
grep -Fq 'TrySendRelease(operationId, "result_timeout")' "$client"
grep -Fq 'server retains the lease until peer disconnect' "$client"

# Contention must finish before any container scan or native StackAll request.
run_block="$(sed -n '/internal static void Run(/,/private static void BeginAfterLeaseGranted/p' "$quick_stack")"
if printf '%s\n' "$run_block" | grep -Eq 'FindAccessibleContainers|container\.StackAll'; then
  printf 'Put Away must acquire the server lease before scanning or native mutation\n' >&2
  exit 1
fi
begin_block="$(sed -n '/private static void BeginAfterLeaseGranted/,/private static QuickStackEligibility/p' "$quick_stack")"
printf '%s\n' "$begin_block" | grep -Fq 'FindAccessibleContainers'

# This owner is only a lease, not a chest transfer or transaction framework.
if rg -n 'Inventory\.StackAll|Container|ClaimOwnership|ForceSendZDO|SetOwner|retry|journal|transaction' "$server" "$state"; then
  printf 'server support must remain a narrow Put Away exclusion lease\n' >&2
  exit 1
fi

dotnet run --project "$mod/tests/put-away-lease/PutAwayLeaseTests.csproj"
dotnet build "$project" --configuration Release
printf 'Benheim server-support Put Away lease checks passed\n'
