#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
quick_stack="$root/src/Inventory/QuickStack.cs"
patches="$root/src/Inventory/QuickStackPatches.cs"
operation="$root/src/Inventory/QuickStackOperation.cs"

# The request must go through Valheim's StackAll ownership handshake. The response
# instrumentation can filter only the active operation; it must not replace movement.
grep -Fq 'container.StackAll();' "$quick_stack"
grep -Fq 'BeginNativeStackResponse' "$quick_stack"
grep -Fq 'CompleteNativeStackResponse' "$quick_stack"
grep -Fq 'ResponseInProgress' "$operation"
grep -Fq 'RequestedContainers' "$operation"
grep -Fq 'HarmonyPatch(typeof(Container), "RPC_StackResponse")' "$patches"
grep -Fq 'HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem)' "$patches"
grep -Fq 'PocketItems.IsPocketed(operation.Player, item)' "$quick_stack"

# Native AddItem/RemoveItem owns transfer semantics. Delta calculation deliberately
# recognizes a partial remainder that remains in the source inventory.
grep -Fq 'source.ContainsItem(item) ? item.m_stack : 0' "$quick_stack"
grep -Fq 'int moved = snapshot.StackBefore - remaining;' "$quick_stack"
if rg -n 'ClaimOwnership|TryBeginDeposit|InventoryTransactions|DepositCandidate' "$quick_stack" "$patches" "$operation"; then
  printf 'native Put Away must not restore custom ownership or transaction layers\n' >&2
  exit 1
fi

printf 'native quick-stack routing and remainder checks passed\n'
