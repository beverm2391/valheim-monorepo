#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
mod="$root/server-mods/benheim-server-support"
server="$mod/src/PutAwayLeaseServer.cs"
state="$mod/src/PutAwayLeaseState.cs"
peer_readiness="$mod/src/PutAwayPeerReadinessState.cs"
plugin="$mod/src/Plugin.cs"
project="$mod/src/BenheimServerSupport.csproj"
runtime="$mod/src/InventoryTransactionRuntime.cs"
kill_server="$mod/src/KillAttributionServer.cs"
kill_state="$mod/src/ConfirmedKillState.cs"
kill_chain="$mod/src/KillChainState.cs"
kill_rules="$root/mods/benheim-qol/src/KillAttribution/KillChainRules.cs"
kill_delivery_attempt="$root/mods/benheim-qol/src/KillAttribution/KillAttributionRpcAttempt.cs"
kill_qualification="$mod/src/VictimQualification.cs"
protocol="$root/shared/benheim-inventory-protocol"
client="$root/mods/benheim-qol/src/Inventory/PutAwayLeaseClient.cs"
quick_stack="$root/mods/benheim-qol/src/Inventory/QuickStack.cs"
quick_stack_completion="$root/mods/benheim-qol/src/Inventory/QuickStackCompletion.cs"

grep -Fq 'PluginName = "Benheim Server Support"' "$plugin"
grep -Fq 'harmony.PatchAll();' "$plugin"
grep -Fq 'InventoryTransactionRuntime.Shutdown();' "$plugin"
grep -Fq 'KillAttributionProtocol.cs' "$project"
grep -Fq 'ReferenceEquals(rpc, reporter.m_rpc)' "$kill_server"
grep -Fq 'victim.GetOwner() != reporter.m_uid' "$kill_server"
grep -Fq 'State.TryConfirm(report.VictimId, report.KillerId, out long sequence)' "$kill_server"
grep -Fq 'lock (sync)' "$kill_state"
grep -Fq 'WindowSeconds = 30d' "$kill_rules"
grep -Fq 'BerserkerKillThreshold = 6' "$kill_rules"
grep -Fq 'SlaughterhouseKillThreshold = 12' "$kill_rules"
grep -Fq 'KillChainRules.WindowSeconds' "$kill_chain"
grep -Fq 'KillChainRules.BerserkerKillThreshold' "$kill_chain"
grep -Fq 'KillChainRules.SlaughterhouseKillThreshold' "$kill_chain"
grep -Fq 'killerPeer.m_rpc.IsConnected()' "$kill_server"
grep -Fq 'KillAttributionRpcAttempt.TrySend(' "$kill_server"
grep -Fq 'KillAttributionProtocol.ChainResetAcknowledgedRpc' "$kill_server"
grep -Fq 'if (!isConnected)' "$kill_delivery_attempt"
grep -Fq 'Character.Faction.ForestMonsters' "$kill_qualification"
grep -Fq 'prefab.GetComponent<MonsterAI>() != null' "$kill_server"
grep -Fq '"Boar".GetStableHashCode()' "$kill_server"
grep -Fq '[HarmonyPatch(typeof(ZNet), "OnNewConnection")]' "$server"
grep -Fq '[HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect), typeof(ZNetPeer))]' "$server"
grep -Fq 'peer.m_rpc.Register<string>(' "$server"
grep -Fq 'ReferenceEquals(rpc, peer.m_rpc)' "$server"
grep -Fq 'Lease.TryAcquireOrValidate(' "$server"
grep -Fq 'PutAwayLeaseRequestDecision.CohortChanged' "$server"
grep -Fq 'Lease.TryReleasePeer(peer, out string operationId)' "$server"
grep -Fq 'PutAwayLeaseProtocol.PeerReadyRpc' "$server"
grep -Fq 'PeerReadiness.Track(peer)' "$server"
grep -Fq 'PeerReadiness.Remove(peer)' "$server"
grep -Fq 'ZNet.instance.GetPeers()' "$server"
grep -Fq 'PeerReadiness.AllConnectedPeersMatch(' "$server"
grep -Fq 'generations.TryGetValue(peer, out int? generation)' "$peer_readiness"
grep -Fq 'rejectionReason = "peer_protocol_unknown"' "$peer_readiness"
grep -Fq 'rejectionReason = "peer_protocol_incompatible"' "$peer_readiness"
lease_request_server_block="$(sed -n '/private static void OnRequest/,/private static void OnRelease/p' "$server")"
peer_gate_line="$(printf '%s\n' "$lease_request_server_block" | grep -n -m1 'PeerReadiness.AllConnectedPeersMatch' | cut -d: -f1)"
lease_acquire_line="$(printf '%s\n' "$lease_request_server_block" | grep -n -m1 'Lease.TryAcquire' | cut -d: -f1)"
if [[ -z "$peer_gate_line" || -z "$lease_acquire_line" || "$peer_gate_line" -ge "$lease_acquire_line" ]]; then
  printf 'peer protocol readiness must be proven before the server grants the global lease\n' >&2
  exit 1
fi
grep -Fq 'lock (sync)' "$state"
grep -Fq 'result.Reason == "busy"' "$quick_stack"
grep -Fq 'Put Away busy — retry in a few seconds' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.TryRequest' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.Release(terminal.Reason)' "$quick_stack_completion"
grep -Fq 'TrySendRelease(operationId, "result_timeout")' "$client"
grep -Fq 'server retains the lease until peer disconnect' "$client"
grep -Fq 'EnsurePeerReadinessSent(serverRpc);' "$client"
request_block="$(sed -n '/internal static bool TryRequest/,/internal static void Update/p' "$client")"
readiness_line="$(printf '%s\n' "$request_block" | grep -n -m1 'EnsurePeerReadinessSent(serverRpc)' | cut -d: -f1)"
lease_request_line="$(printf '%s\n' "$request_block" | grep -n -m1 'serverRpc.Invoke(PutAwayLeaseProtocol.RequestRpc' | cut -d: -f1)"
if [[ -z "$readiness_line" || -z "$lease_request_line" || "$readiness_line" -ge "$lease_request_line" ]]; then
  printf 'the requester must announce peer readiness before sending its lease request\n' >&2
  exit 1
fi

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
grep -Fq 'InventoryTransactions.RemoveServerRequester(peer.m_uid);' "$runtime"
grep -Fq 'new InventoryTransactionDiagnosticSink(Plugin.Log)' "$runtime"
grep -Fq 'InventoryTransactionDiagnosticProjection.EmitBestEffort(diagnosticSink, diagnosticEvent)' "$protocol/InventoryTransactions.cs"
grep -Fq 'ServerRouter.RemoveRequester(requester)' "$protocol/InventoryTransactions.cs"
grep -Fq 'long owner = ResolveOwner(containerId);' "$protocol/InventoryTransactionServer.cs"
grep -Fq 'view.IsOwner()' "$protocol/InventoryTransactionOwner.cs"
grep -Fq 'target.AddItem(item.Clone())' "$protocol/InventoryTransactionOwner.cs"

owner_completion_block="$(sed -n '/InventoryTransactionReceipts.Record/,/InventoryTransactions.Emit(resultEvent);/p' \
    "$protocol/InventoryTransactionOwner.cs")"
owner_send_line="$(printf '%s\n' "$owner_completion_block" | grep -n -m1 'SendResult(requester' | cut -d: -f1)"
owner_emit_line="$(printf '%s\n' "$owner_completion_block" | grep -n -m1 'InventoryTransactions.Emit(resultEvent)' | cut -d: -f1)"
if [[ -z "$owner_send_line" || -z "$owner_emit_line" || "$owner_send_line" -ge "$owner_emit_line" ]]; then
  printf 'owner result delivery must precede result telemetry\n' >&2
  exit 1
fi

client_completion_block="$(sed -n '/ClientPending.Remove(transactionId);/,/private static void EmitSettledResult/p' \
    "$protocol/InventoryTransactionClient.cs")"
client_callback_line="$(printf '%s\n' "$client_completion_block" | grep -n -m1 'pending.Callback(result)' | cut -d: -f1)"
client_emit_line="$(printf '%s\n' "$client_completion_block" | grep -n -m1 'EmitSettledResult' | cut -d: -f1)"
if [[ -z "$client_callback_line" || -z "$client_emit_line" || "$client_callback_line" -ge "$client_emit_line" ]]; then
  printf 'requester completion must precede settlement telemetry\n' >&2
  exit 1
fi

if rg -n -g '*.cs' 'ClaimOwnership|Inventory\.StackAll' "$protocol"; then
  printf 'shared Put Away protocol must not write a requester-local chest cache\n' >&2
  exit 1
fi

dotnet run --project "$mod/tests/put-away-lease/PutAwayLeaseTests.csproj"
dotnet run --project "$root/tests/put-away-owner-routing/PutAwayOwnerRoutingTests.csproj"
dotnet run --project "$root/tests/inventory-transaction-receipts/InventoryTransactionReceiptTests.csproj"
dotnet run --project "$root/tests/put-away-receipt-ack/PutAwayReceiptAckTests.csproj"
dotnet run --project "$root/tests/put-away-protocol-compatibility/PutAwayProtocolCompatibilityTests.csproj"
dotnet run --project "$root/tests/inventory-transaction-diagnostics/InventoryTransactionDiagnosticTests.csproj"
dotnet run --project "$root/tests/kill-attribution/KillAttributionTests.csproj"
dotnet build "$project" --configuration Release
printf 'Benheim server-support Put Away lease checks passed\n'
