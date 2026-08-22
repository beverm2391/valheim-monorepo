#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
repo_root="$(cd "$root/../.." && pwd)"
quick_stack="$root/src/Inventory/QuickStack.cs"
lease_validation="$root/src/Inventory/QuickStackLeaseValidation.cs"
lease_client="$root/src/Inventory/PutAwayLeaseClient.cs"
patches="$root/src/Inventory/QuickStackPatches.cs"
client="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionClient.cs"
models="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionModels.cs"
server="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionServer.cs"
owner="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionOwner.cs"
owner_receipt_cleanup="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionOwnerReceiptCleanup.cs"
wire="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionWire.cs"
composition="$repo_root/shared/benheim-inventory-protocol/InventoryTransactions.cs"
protocol="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionProtocol.cs"
routing="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionRoutingCore.cs"
settlement="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionSettlement.cs"
diagnostics="$repo_root/shared/benheim-inventory-protocol/InventoryTransactionDiagnostics.cs"
stage_timing="$repo_root/shared/benheim-inventory-protocol/PutAwayStageTiming.cs"
runtime="$root/src/Inventory/InventoryTransactionRuntime.cs"
diagnostic_sink="$root/src/Inventory/InventoryTransactionDiagnosticSink.cs"
scheduler="$root/src/Inventory/QuickStackBatchScheduler.cs"
pipeline="$root/src/Inventory/QuickStackBatchPipeline.cs"
transfer="$root/src/Inventory/QuickStackTransfer.cs"

# The requester reserves first and never writes its cached destination chest.
grep -Fq 'InventoryTransactions.TryBeginDeposit' "$lease_validation"
grep -Fq 'PutAwayLeaseClient.TryValidate(' "$quick_stack"
request_next_block="$(sed -n '/private static void RequestNextContainer/,/private static void ApplyContainerResult/p' "$quick_stack")"
if printf '%s\n' "$request_next_block" | grep -Fq 'InventoryTransactions.TryBeginDeposit'; then
  printf 'Put Away must validate the active lease before each source reservation\n' >&2
  exit 1
fi
validation_block="$(sed -n '/private static void BeginDepositAfterLeaseValidation/,/private static void CancelBeforeReservation/p' "$lease_validation")"
stale_validation_block="$(printf '%s\n' "$validation_block" | sed -n '1,/reason=stale_validation_result/p')"
if printf '%s\n' "$stale_validation_block" | grep -Eq 'container == null|candidates == null|!container|!operation.Player'; then
  printf 'destroyed or missing validation context must cancel the active batch, not return as stale\n' >&2
  exit 1
fi
context_loss_line="$(printf '%s\n' "$validation_block" | grep -n -m1 'if (!container' | cut -d: -f1)"
context_cancel_line="$(printf '%s\n' "$validation_block" | grep -n -m1 'CancelBeforeReservation(operation, "validation_context_unavailable")' | cut -d: -f1)"
validation_reject_line="$(printf '%s\n' "$validation_block" | grep -n -m1 '!leaseResult.Granted' | cut -d: -f1)"
reservation_line="$(printf '%s\n' "$validation_block" | grep -n -m1 'InventoryTransactions.TryBeginDeposit' | cut -d: -f1)"
if [[ -z "$context_loss_line" || -z "$context_cancel_line" || -z "$validation_reject_line" || -z "$reservation_line" \
    || "$context_loss_line" -ge "$context_cancel_line" || "$context_cancel_line" -ge "$reservation_line" \
    || "$validation_reject_line" -ge "$reservation_line" ]]; then
  printf 'a failed cohort validation must stop before inventory reservation\n' >&2
  exit 1
fi
cancel_block="$(sed -n '/private static void CancelBeforeReservation/,$p' "$lease_validation")"
grep -Fq 'operation.Pipeline.StopScheduling("cancelled", reason);' <<<"$cancel_block"
if grep -Fq 'PutAwayLeaseClient.Release(reason);' <<<"$cancel_block"; then
  printf 'failed validation must drain in-flight deposits before releasing the lease\n' >&2
  exit 1
fi
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
grep -Fq 'InventoryTransactionProtocol.OwnerExecuteRpc' "$server"
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
grep -Fq 'protocolVersion != InventoryTransactionProtocol.Version' "$wire"
grep -Fq 'Version = 4' "$protocol"

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
grep -Fq 'operation.Pipeline.TryBeginValidatedDeposit(' "$lease_validation"
grep -Fq 'QuickStackTransfer.HasLaterCandidateDependency(' "$lease_validation"
grep -Fq 'QuickStackDepositContinuation continuation =' "$lease_validation"
grep -Fq 'continuation.CompleteBegin(began);' "$lease_validation"
grep -Fq 'QuickStackBatchDependencies.HasItemNameOverlap(' "$transfer"
grep -Fq 'depositSettled();' "$pipeline"
grep -Fq 'operation.Pipeline.StopScheduling("cancelled", "container_scheduling_failed");' "$quick_stack"
grep -Fq 'inFlight.Remove(ticket)' "$scheduler"
grep -Fq '!schedulingStopped || inFlight.Count != 0 || terminalTaken' "$scheduler"
grep -Fq 'scheduler.TryTakeTerminal(out QuickStackBatchTerminal? terminal)' "$pipeline"
grep -Fq 'terminalReady(terminal!);' "$pipeline"
grep -Fq 'PutAwayLeaseClient.Release(terminal.Reason);' "$root/src/Inventory/QuickStackCompletion.cs"
grep -Fq 'bool retainHeldLeaseForBatchDrain =' "$lease_client"
grep -Fq 'wasValidation && heldOperationId == operationId;' "$lease_client"
grep -Fq 'client_receipt_ack_sent' "$client"
grep -Fq 'InventoryTransactionReceiptAcknowledgementCodec.TryAuthorize(' "$server"
grep -Fq 'pair.Value.CompletedAt < olderThan' "$routing"
if rg -n 'ReceiptAckResultRpc|OwnerReceiptAckResultRpc|MarkReceiptAcknowledged|AcknowledgedAt|pending\.Settled' \
    "$client" "$server" "$owner" "$owner_receipt_cleanup" "$composition" "$routing"; then
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
grep -Fq '.Number("batch_duration_ms", batchDurationMs)' "$composition"
grep -Fq '.Number("scan_match_duration_ms", scanMatchDurationMs)' "$composition"
grep -Fq 'routing_owner_handoff_duration_ms' "$client"
grep -Fq 'RoutingOwnerHandoffStartedAt = PutAwayStageTiming.Start();' "$models"
grep -Fq 'requester_settlement_duration_ms' "$client"
grep -Fq 'owner_mutation_duration_ms' "$owner"
grep -Fq 'Stopwatch.GetTimestamp()' "$stage_timing"
routing_stop_line="$(grep -nF 'pending.RoutingOwnerHandoffDurationMs ??=' "$client" | cut -d: -f1)"
settlement_availability_line="$(grep -nF 'InventoryTransactionLifecyclePolicy.CanSettle(localPlayer)' "$client" | cut -d: -f1)"
if [[ -z "$routing_stop_line" || -z "$settlement_availability_line" \
    || "$routing_stop_line" -ge "$settlement_availability_line" ]]; then
  printf 'routing/owner handoff timing must stop before requester settlement can defer\n' >&2
  exit 1
fi
contents_snapshot_line="$(grep -nF 'string contentsBefore = InventoryTransactions.DescribeInventory' "$owner" | cut -d: -f1)"
owner_timing_start_line="$(grep -nF 'long ownerMutationStartedAt = PutAwayStageTiming.Start();' "$owner" | cut -d: -f1)"
receipt_record_line="$(grep -nF 'InventoryTransactionReceipts.Record' "$owner" | tail -1 | cut -d: -f1)"
owner_timing_stop_line="$(grep -nF 'long ownerMutationCompletedAt = PutAwayStageTiming.Start();' "$owner" | cut -d: -f1)"
owner_result_send_line="$(grep -nF 'SendResult(requester, InventoryTransactionWire.BuildResponse(' "$owner" | tail -1 | cut -d: -f1)"
if [[ -z "$contents_snapshot_line" || -z "$owner_timing_start_line" || -z "$receipt_record_line" \
    || -z "$owner_timing_stop_line" || -z "$owner_result_send_line" \
    || "$contents_snapshot_line" -ge "$owner_timing_start_line" \
    || "$owner_timing_start_line" -ge "$receipt_record_line" \
    || "$receipt_record_line" -ge "$owner_timing_stop_line" \
    || "$owner_timing_stop_line" -ge "$owner_result_send_line" ]]; then
  printf 'owner mutation timing must exclude diagnostic snapshots and outbound result delivery\n' >&2
  exit 1
fi
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
dotnet run --project "$repo_root/tests/put-away-protocol-compatibility/PutAwayProtocolCompatibilityTests.csproj"
dotnet run --project "$repo_root/tests/inventory-transaction-diagnostics/InventoryTransactionDiagnosticTests.csproj"
dotnet run --project "$repo_root/tests/put-away-batch-scheduler/PutAwayBatchSchedulerTests.csproj"

printf 'owner-authoritative Put Away source and conservation checks passed\n'
