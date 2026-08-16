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

printf 'owner-authoritative Put Away source and conservation checks passed\n'
