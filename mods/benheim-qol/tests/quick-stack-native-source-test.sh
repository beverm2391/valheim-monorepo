#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(cd "$root/../.." && pwd)"
quick_stack="$root/src/Inventory/QuickStack.cs"
patches="$root/src/Inventory/QuickStackPatches.cs"
client="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionClient.cs"
client_receipt="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionClientReceipt.cs"
server="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionServer.cs"
owner="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionOwner.cs"
wire="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionWire.cs"
composition="$repo_root/shared/benheim-inventory-protocol/InventoryTransactions.cs"
routing="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionRoutingCore.cs"
settlement="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionSettlement.cs"
diagnostics="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionDiagnostics.cs"
runtime="$root/src/Inventory/InventoryTransactionRuntime.cs"
diagnostic_sink="$root/src/Inventory/InventoryTransactionDiagnosticSink.cs"

# The requester reserves first and never writes its cached destination chest.
grep -Fq 'InventoryTransactions.TryBeginDeposit' "$quick_stack"
grep -Fq 'source.RemoveItem(sourceItem, sourceItem.m_stack)' "$client"
grep -Fq 'SendDepositRequest(pending);' "$client"
remove_line="$(grep -nF 'source.RemoveItem(sourceItem, sourceItem.m_stack)' "$client" | cut -d: -f1)"
send_line="$(grep -nF 'SendDepositRequest(pending);' "$client" | head -1 | cut -d: -f1)"
if (( remove_line >= send_line )); then
  printf 'Put Away must reserve before sending the owner-routed request\n' >&2
  exit 1
fi

# The server routes the immutable request to the current ZDO owner. Only an
# instance that still owns the chest can validate and mutate it.
grep -Fq 'string payloadHash = InventoryTransactionWire.Hash(requestBytes);' "$server"
grep -Fq 'long owner = ResolveOwner(containerId);' "$server"
grep -Fq 'ServerRouter.ReceiveRequest' "$server"
grep -Fq 'InvokeRoutedRPC(decision.Owner, OwnerExecuteRpc, envelope)' "$server"
grep -Fq 'sender != route.RoutedOwner' "$routing"
grep -Fq 'sender != currentOwner' "$routing"
grep -Fq 'view.IsOwner()' "$owner"
grep -Fq 'target.AddItem(item.Clone())' "$owner"
grep -Fq 'InventoryTransactionReceipts.Record' "$owner"
receipt_read_line="$(grep -nF 'InventoryTransactionReceipts.TryRead' "$owner" | head -1 | cut -d: -f1)"
apply_line="$(grep -nF '? ApplyDeposit(' "$owner" | head -1 | cut -d: -f1)"
receipt_record_line="$(grep -nF 'InventoryTransactionReceipts.Record' "$owner" | head -1 | cut -d: -f1)"
send_result_line="$(grep -nF 'SendResult(requester, InventoryTransactionWire.BuildResponse(' "$owner" | tail -1 | cut -d: -f1)"
if (( receipt_read_line >= apply_line || receipt_record_line >= send_result_line )); then
  printf 'owner receipts must deduplicate before apply and persist before result\n' >&2
  exit 1
fi

# Correlation and exact accepted counts own requester refunds and connected
# retry deduplication.
grep -Fq 'pending.PayloadHash != payloadHash' "$client"
grep -Fq 'RestoreRemainder' "$client"
grep -Fq 'InventoryTransactionSettlement.TryCreate' "$client"
grep -Fq 'rejected[index] = reserved[index] - accepted[index]' "$settlement"
grep -Fq 'ClientPending.TryGetValue(transactionId' "$client"
grep -Fq 'ConnectedTransactionRouter<ZDOID> ServerRouter' "$composition"
grep -Fq 'InventoryTransactionReceipts.TryRead' "$owner"
grep -Fq 'protocolVersion != InventoryTransactions.ProtocolVersion' "$wire"
grep -Fq 'ProtocolVersion = 3' "$composition"

# Put Away completes exact settlement, receives the current owner's receipt
# acknowledgement through the server, then invokes Quick Stack's completion
# callback. A filled inventory uses the
# emergency nearby-drop path and visible one-shot warning. Valheim retains its
# native save lifecycle.
grep -Fq 'InventoryTransactionSettlement completedSettlement' "$client"
grep -Fq 'TrySendSettledReceiptAck(pending);' "$client"
ack_line="$(grep -nF 'private static void RpcReceiptAckResult' "$client_receipt" | cut -d: -f1)"
remove_pending_line="$(grep -nF 'ClientPending.Remove(pending.TransactionId);' "$client_receipt" | cut -d: -f1)"
callback_line="$(grep -nF 'pending.Callback(settled.Result);' "$client_receipt" | cut -d: -f1)"
if (( ack_line >= remove_pending_line || remove_pending_line >= callback_line )); then
  printf 'Put Away success must follow settlement and current-owner receipt acknowledgement\n' >&2
  exit 1
fi
grep -Fq 'settled_receipt_acknowledged' "$client_receipt"
grep -Fq 'client_receipt_ack_pending' "$client_receipt"
grep -Fq 'ReceiptAckResultRpc' "$composition"
grep -Fq 'OwnerReceiptAckResultRpc' "$composition"
grep -Fq 'ServerRouter.MatchesCompleted' "$server"
grep -Fq 'ServerRouter.MarkReceiptAcknowledged' "$server"
grep -Fq 'pair.Value.AcknowledgedAt.HasValue' "$routing"
grep -Fq 'if (pending.Settled != null)' "$client"
grep -Fq 'duplicate_result_after_settlement' "$client"
retry_line="$(grep -nF 'private static void RetryClientTransactions' "$client" | cut -d: -f1)"
settled_retry_line="$(tail -n +"$retry_line" "$client" | grep -nF 'if (pending.Settled != null)' | head -1 | cut -d: -f1)"
deposit_retry_line="$(tail -n +"$retry_line" "$client" | grep -nF 'SendDepositRequest(pending);' | head -1 | cut -d: -f1)"
if (( settled_retry_line >= deposit_retry_line )); then
  printf 'settled deposits must retry only receipt acknowledgement before request retry\n' >&2
  exit 1
fi
grep -Fq 'InventoryTransactionRefundPlacement.WorldDrop' "$client"
grep -Fq 'Put Away refund dropped nearby. Pick it up.' "$diagnostic_sink"
if rg -n 'SavePlayerProfile|client_save_pending|client_persisted|owner_accepted_save_pending' "$client"; then
  printf 'Put Away must preserve Valheim native character-save lifecycle\n' >&2
  exit 1
fi

# The client and server share one complete typed protocol event model.
# Lease events are non-terminal; only the Put Away batch start/finish events
# define query lifecycle.
grep -Fq 'InventoryTransactionDiagnosticEvent.Domain' "$root/src/Inventory/InventoryTransactionDiagnosticSink.cs"
grep -Fq 'InventoryTransactions.Initialize(' "$runtime"
grep -Fq 'InventoryTransactions.Update();' "$runtime"
grep -Fq 'InventoryTransactions.Shutdown();' "$runtime"
grep -Fq 'internal const string Domain = "InventoryTransaction"' "$diagnostics"
grep -Fq 'InventoryTransactionDiagnosticEvent.Create("put_away_batch_started"' "$composition"
grep -Fq 'InventoryTransactionDiagnosticEvent.Create("put_away_batch_finished"' "$composition"
grep -Fq '.Code("operation_phase", "start")' "$composition"
grep -Fq '.Code("operation_phase", "terminal")' "$composition"

# Rejected local shortcuts stay deleted. Native Stack All remains patched only
# for ordinary pocket-item filtering, never as Put Away's transfer protocol.
if rg -n 'ClaimOwnership|container\.StackAll\(\)|QuickStackContainerWrite|QuickStackResponseGuard' \
    "$quick_stack" "$client" "$server" "$owner"; then
  printf 'requester-local or uncorrelated Put Away machinery returned\n' >&2
  exit 1
fi
grep -Fq 'HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll)' "$patches"
grep -Fq 'PocketItems.IsPocketed(scope.Player, item)' "$quick_stack"

dotnet run --project "$repo_root/tests/put-away-owner-routing/PutAwayOwnerRoutingTests.csproj"
dotnet run --project "$repo_root/tests/inventory-transaction-receipts/InventoryTransactionReceiptTests.csproj"
dotnet run --project "$repo_root/tests/inventory-transaction-diagnostics/InventoryTransactionDiagnosticTests.csproj"

printf 'owner-authoritative Put Away source and conservation checks passed\n'
