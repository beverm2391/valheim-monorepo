#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
quick_stack="$root/src/Inventory/QuickStack.cs"
quick_stack_transfer="$root/src/Inventory/QuickStackTransfer.cs"
patches="$root/src/Inventory/QuickStackPatches.cs"
operation="$root/src/Inventory/QuickStackOperation.cs"
response_guard="$root/src/Inventory/QuickStackResponseGuard.cs"
container_write="$root/src/Inventory/QuickStackContainerWrite.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"

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
grep -Fq 'TopLeftFeedbackHud.ShowTransient("Put Away timed out — reconnect to safely retry this chest")' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.TryRequest' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.Release("native_response_timeout")' "$quick_stack"
grep -Fq 'networkView.ClaimOwnership();' "$container_write"
grep -Fq 'scope.ContainerWrite?.Complete(movedItems);' "$quick_stack"
grep -Fq 'return QuickStack.ShouldRunBulkStack(__state);' "$patches"
grep -Fq 'if (!scope.AllowNativeStack)' "$quick_stack"
claim_block="$(sed -n '/bool accountsForPutAway/,/QuickStackBulkScope scope/p' "$quick_stack")"
printf '%s\n' "$claim_block" | grep -Fq 'QuickStackContainerWrite.TryBegin'
grep -Fq 'if (!m_loading && IsOwner())' "$source_tree/Container.cs"
take_all_block="$(sed -n '/private void RPC_TakeAllRespons/,/private void OnContainerChanged/p' "$source_tree/Container.cs")"
printf '%s\n' "$take_all_block" | grep -Fq 'm_nview.ClaimOwnership();'
stack_all_block="$(sed -n '/private void RPC_StackResponse/,/^\t}/p' "$source_tree/Container.cs")"
if printf '%s\n' "$stack_all_block" | grep -Fq 'ClaimOwnership'; then
  printf 'native Stack All unexpectedly gained the ownership safeguard; reassess Benheim wrapper\n' >&2
  exit 1
fi

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
grep -Fq 'scope.Player.GetInventory().ContainsItem(snapshot.Item)' "$quick_stack_transfer"
grep -Fq 'int moved = snapshot.StackBefore - remaining;' "$quick_stack_transfer"
if rg -n 'ClaimOwnership|TryBeginDeposit|InventoryTransactions|DepositCandidate|RequestedContainers|ResponseInProgress|ResponseTimeout|AbandonedStackResponse|OrdinaryStackAllRequest' "$quick_stack" "$patches" "$operation"; then
  printf 'native Put Away must not restore custom ownership or transaction layers\n' >&2
  exit 1
fi

printf 'native quick-stack routing and remainder checks passed\n'
