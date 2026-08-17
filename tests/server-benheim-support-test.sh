#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mod="$root/server-mods/benheim-server-support"
server="$mod/src/PutAwayLeaseServer.cs"
state="$mod/src/PutAwayLeaseState.cs"
plugin="$mod/src/Plugin.cs"
project="$mod/src/BenheimServerSupport.csproj"
runtime="$mod/src/InventoryTransactionRuntime.cs"
kill_server="$mod/src/KillAttributionServer.cs"
kill_state="$mod/src/ConfirmedKillState.cs"
protocol="$root/shared/benheim-inventory-protocol"
client="$root/mods/benheim-qol/src/Inventory/PutAwayLeaseClient.cs"
quick_stack="$root/mods/benheim-qol/src/Inventory/QuickStack.cs"

grep -Fq 'PluginName = "Benheim Server Support"' "$plugin"
grep -Fq 'harmony.PatchAll();' "$plugin"
grep -Fq 'InventoryTransactionRuntime.Shutdown();' "$plugin"
grep -Fq 'KillAttributionProtocol.cs' "$project"
grep -Fq 'ReferenceEquals(rpc, reporter.m_rpc)' "$kill_server"
grep -Fq 'victim.GetOwner() != reporter.m_uid' "$kill_server"
grep -Fq 'State.TryConfirm(report.VictimId, report.KillerId, out long sequence)' "$kill_server"
grep -Fq 'lock (sync)' "$kill_state"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnNewConnection")]' "$server"
grep -Fq '[HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect), typeof(ZNetPeer))]' "$server"
grep -Fq 'peer.m_rpc.Register<string>(' "$server"
grep -Fq 'ReferenceEquals(rpc, peer.m_rpc)' "$server"
grep -Fq 'Lease.TryAcquire(peer, safeOperationId)' "$server"
grep -Fq 'Lease.TryReleasePeer(peer, out string operationId)' "$server"
grep -Fq 'lock (sync)' "$state"
grep -Fq 'result.Reason == "busy"' "$quick_stack"
grep -Fq 'Put Away busy — retry in a few seconds' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.TryRequest' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.Release("batch_finished")' "$quick_stack"
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

# The lease stays independent from chest contents. Owner-authoritative deposits
# are routed by the separate shared protocol.
if rg -n 'Inventory\.StackAll|Container|ClaimOwnership|ForceSendZDO|SetOwner|transaction' "$server" "$state"; then
  printf 'the global lease must not become the chest transaction implementation\n' >&2
  exit 1
fi
grep -Fq 'shared/benheim-inventory-protocol/*.cs' "$project"
grep -Fq '[HarmonyPatch(typeof(ZNet), "Awake")]' "$runtime"
grep -Fq '[HarmonyPatch(typeof(ZNet), "Update")]' "$runtime"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnDestroy")]' "$runtime"
grep -Fq 'InventoryTransactions.Initialize(' "$runtime"
grep -Fq 'InventoryTransactions.Update();' "$runtime"
grep -Fq 'InventoryTransactions.Shutdown();' "$runtime"
grep -Fq 'new InventoryTransactionDiagnosticSink(Plugin.Log)' "$runtime"
grep -Fq 'long owner = ResolveOwner(containerId);' "$protocol/InventoryTransactionServer.cs"
grep -Fq 'view.IsOwner()' "$protocol/InventoryTransactionOwner.cs"
grep -Fq 'target.AddItem(item.Clone())' "$protocol/InventoryTransactionOwner.cs"
if rg -n -g '*.cs' 'ClaimOwnership|Inventory\.StackAll' "$protocol"; then
  printf 'shared Put Away protocol must not write a requester-local chest cache\n' >&2
  exit 1
fi

dotnet run --project "$mod/tests/put-away-lease/PutAwayLeaseTests.csproj"
dotnet run --project "$root/tests/put-away-owner-routing/PutAwayOwnerRoutingTests.csproj"
dotnet run --project "$root/tests/inventory-transaction-receipts/InventoryTransactionReceiptTests.csproj"
dotnet run --project "$root/tests/inventory-transaction-diagnostics/InventoryTransactionDiagnosticTests.csproj"
dotnet run --project "$root/tests/kill-attribution/KillAttributionTests.csproj"
dotnet build "$project" --configuration Release
printf 'Benheim server-support Put Away lease checks passed\n'
