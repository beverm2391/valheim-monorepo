#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
quick_stack="$root/src/Inventory/QuickStack.cs"
patches="$root/src/Inventory/QuickStackPatches.cs"
operation="$root/src/Inventory/QuickStackOperation.cs"
response_guard="$root/src/Inventory/QuickStackResponseGuard.cs"

# Container ownership remains native; every local Inventory.StackAll shares the filter.
grep -Fq 'container.StackAll();' "$quick_stack"
grep -Fq 'HarmonyPatch(typeof(Container), "RPC_StackResponse")' "$patches"
grep -Fq 'HarmonyPatch(typeof(Inventory), nameof(Inventory.StackAll)' "$patches"
grep -Fq 'HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem)' "$patches"
grep -Fq 'QuickStackBulkScope.Active' "$quick_stack"
grep -Fq 'PocketItems.IsPocketed(scope.Player, item)' "$quick_stack"
grep -Fq 'operation.Player == player' "$quick_stack"
grep -Fq 'RestoreBulkScope(scope);' "$quick_stack"
grep -Fq 'QuickStackBulkScope.Active == scope' "$quick_stack"
grep -Fq 'QuickStackBulkScope? Previous' "$operation"
grep -Fq 'TryHandleNativeDenial' "$quick_stack"
grep -Fq 'FinalizeBulkStack' "$quick_stack"
grep -Fq 'QuickStack.ResetState();' "$root/src/Plugin.cs"
grep -Fq 'QuickStack.Update();' "$root/src/Plugin.cs"
grep -Fq 'QuickStackResponseGuard<Container>' "$quick_stack"
grep -Fq 'TryTimeoutRequest(Time.unscaledTime' "$quick_stack"
grep -Fq 'quick_stack_late_response_discarded' "$quick_stack"
grep -Fq 'TryHandleTimedOutResponse' "$patches"
grep -Fq 'TryDiscardTimedOutResponse' "$response_guard"
grep -Fq 'IsWaitingForTimedOutResponse' "$response_guard"
grep -Fq 'TopLeftFeedbackHud.ShowTransient("Put Away timed out; try again")' "$quick_stack"

# A timeout clears the batch without issuing another native request. The one
# timed-out chest remains unavailable until the RPC prefix consumes its response.
timeout_block="$(sed -n '/internal static void Update()/,/internal static bool TryHandleTimedOutResponse/p' "$quick_stack")"
printf '%s\n' "$timeout_block" | grep -Fq 'activeOperation = null;'
if printf '%s\n' "$timeout_block" | grep -Fq 'RequestNextContainer();'; then
  printf 'a missing native response must cancel rather than continue Put Away\n' >&2
  exit 1
fi
response_prefix="$(sed -n '/private static bool Prefix(Container __instance, bool granted)/,/^        }/p' "$patches")"
printf '%s\n' "$response_prefix" | grep -Fq 'TryHandleTimedOutResponse(__instance, granted)'
denial_block="$(sed -n '/internal static bool TryHandleNativeDenial/,/internal static QuickStackBulkScope/p' "$quick_stack")"
printf '%s\n' "$denial_block" | grep -Fq 'ResponseGuard.CompleteCurrentResponse(container);'
granted_block="$(sed -n '/internal static void CompleteBulkStack/,/internal static System.Exception/p' "$quick_stack")"
printf '%s\n' "$granted_block" | grep -Fq 'ResponseGuard.CompleteCurrentResponse(container);'

# Native AddItem/RemoveItem owns transfer semantics. Delta calculation deliberately
# recognizes a partial remainder that remains in the source inventory.
grep -Fq 'scope.Player.GetInventory().ContainsItem(snapshot.Item)' "$quick_stack"
grep -Fq 'int moved = snapshot.StackBefore - remaining;' "$quick_stack"
if rg -n 'ClaimOwnership|TryBeginDeposit|InventoryTransactions|DepositCandidate|RequestedContainers|ResponseInProgress|ResponseTimeout|AbandonedStackResponse|OrdinaryStackAllRequest' "$quick_stack" "$patches" "$operation"; then
  printf 'native Put Away must not restore custom ownership or transaction layers\n' >&2
  exit 1
fi

printf 'native quick-stack routing and remainder checks passed\n'
