#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(cd "$root/../.." && pwd)"
quick_stack="$root/src/Inventory/QuickStack.cs"
patches="$root/src/Inventory/QuickStackPatches.cs"
client="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionClient.cs"
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

# Put Away completes immediately after exact settlement. One-way receipt
# cleanup follows, but it cannot retain Quick Stack or the
# global lease. A filled inventory uses the
# emergency nearby-drop path and visible one-shot warning. Valheim retains its
# native save lifecycle.
grep -Fq 'InventoryTransactionSettlement completedSettlement' "$client"
grep -Fq 'ClientPending.Remove(transactionId);' "$client"
grep -Fq 'TrySendReceiptAcknowledgement(pending);' "$client"
grep -Fq 'pending.Callback(result);' "$client"
remove_pending_line="$(grep -nF 'ClientPending.Remove(transactionId);' "$client" | cut -d: -f1)"
callback_line="$(grep -nF 'pending.Callback(result);' "$client" | cut -d: -f1)"
cleanup_line="$(grep -nF 'TrySendReceiptAcknowledgement(pending);' "$client" | cut -d: -f1)"
if (( remove_pending_line >= callback_line || callback_line >= cleanup_line )); then
  printf 'exact settlement must clear the pending request before Put Away completion\n' >&2
  exit 1
fi
grep -Fq 'finally' "$client"
grep -Fq 'ContinueAfterSettledContainer(operation);' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.Release("container_completion_failed");' "$quick_stack"
grep -Fq 'client_receipt_ack_sent' "$client"
grep -Fq 'InventoryTransactionReceiptAcknowledgementCodec.TryAuthorize(' "$server"
grep -Fq 'pair.Value.CompletedAt < olderThan' "$routing"
if rg -n 'ReceiptAckResultRpc|OwnerReceiptAckResultRpc|MarkReceiptAcknowledged|AcknowledgedAt|pending\.Settled' \
    "$client" "$server" "$owner" "$composition" "$routing"; then
  printf 'receipt cleanup must stay one-way and outside transaction state\n' >&2
  exit 1
fi
if rg -n 'RequestBelongsToSender|Player\.GetAllPlayers' "$server"; then
  printf 'routed sender identity must not depend on dedicated-server Player scene objects\n' >&2
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
dotnet run --project "$repo_root/tests/put-away-receipt-ack/PutAwayReceiptAckTests.csproj"
dotnet run --project "$repo_root/tests/inventory-transaction-diagnostics/InventoryTransactionDiagnosticTests.csproj"

printf 'owner-authoritative Put Away source and conservation checks passed\n'
