#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
quick_stack="$root/src/Inventory/QuickStack.cs"
patches="$root/src/Inventory/QuickStackPatches.cs"
operation="$root/src/Inventory/QuickStackOperation.cs"

# Container ownership remains native; every local Inventory.StackAll shares the filter.
grep -Fq 'container.StackAll();' "$quick_stack"
grep -Fq 'HarmonyPatch(typeof(Container), "RPC_StackResponse")' "$patches"
grep -Fq 'HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll)' "$patches"
grep -Fq 'HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem)' "$patches"
grep -Fq 'QuickStackBulkScope.Active' "$quick_stack"
grep -Fq 'PocketItems.IsPocketed(scope.Player, item)' "$quick_stack"
grep -Fq 'AccountsForPutAway' "$quick_stack"
grep -Fq 'operation.Player == player' "$quick_stack"
grep -Fq 'RestoreBulkScope(scope);' "$quick_stack"
grep -Fq 'QuickStackBulkScope.Active == scope' "$quick_stack"
grep -Fq 'QuickStackBulkScope? Previous' "$operation"
grep -Fq 'TryHandleNativeDenial' "$quick_stack"
grep -Fq 'FinalizeBulkStack' "$quick_stack"
grep -Fq 'QuickStack.ResetState();' "$root/src/Plugin.cs"

# Native AddItem/RemoveItem owns transfer semantics. Delta calculation deliberately
# recognizes a partial remainder that remains in the source inventory.
grep -Fq 'scope.Source.ContainsItem(snapshot.Item)' "$quick_stack"
grep -Fq 'int moved = snapshot.StackBefore - remaining;' "$quick_stack"
if rg -n 'ClaimOwnership|TryBeginDeposit|InventoryTransactions|DepositCandidate|RequestedContainers|ResponseInProgress|ResponseTimeout|AbandonedStackResponse|OrdinaryStackAllRequest' "$quick_stack" "$patches" "$operation"; then
  printf 'native Put Away must not restore custom ownership or transaction layers\n' >&2
  exit 1
fi

printf 'native quick-stack routing and remainder checks passed\n'
