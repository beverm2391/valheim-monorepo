#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
feedback="$root/src/Infrastructure/WorldFeedback.cs"
marker="$root/src/Inventory/PocketMarker.cs"
controller="$root/src/Inventory/PocketItemController.cs"
protection="$root/src/Inventory/PocketItems.cs"
quick_stack="$root/src/Inventory/QuickStack.cs"
quick_stack_availability="$root/src/Inventory/QuickStackAvailability.cs"
quick_stack_diagnostics="$root/src/Inventory/QuickStackDiagnostics.cs"
quick_stack_location="$root/src/Inventory/QuickStackLocation.cs"
quick_stack_feedback="$root/src/Inventory/QuickStackFeedback.cs"
quick_stack_summary="$root/src/Inventory/QuickStackSummary.cs"
quick_stack_receipt_hud="$root/src/Inventory/QuickStackReceiptHud.cs"
visibility="$root/src/Inventory/InventoryVisibility.cs"
protocol_root="$root/../../shared/benheim-inventory-protocol"
protocol_core="$protocol_root/InventoryTransactions.cs"
protocol_client="$protocol_root/InventoryTransactionClient.cs"
protocol_server="$protocol_root/InventoryTransactionServer.cs"
protocol_owner="$protocol_root/InventoryTransactionOwner.cs"
protocol_receipts="$protocol_root/InventoryTransactionReceipts.cs"

grep -Fq 'AddInworldText' "$feedback"
grep -Fq 'UtilityTextDurationSeconds = 3f' "$feedback"
grep -Fq 'DurationField?.SetValue' "$feedback"
if grep -Fq '.ShowText(' "$feedback"; then
  printf 'world feedback must stay local instead of using broadcast damage text\n' >&2
  exit 1
fi

grep -Fq 'IsAutomaticallyProtected' "$protection"
grep -Fq 'InstancePocketKey = "com.benheim.qol:pocketed"' "$protection"
grep -Fq 'm_maxStackSize > 1' "$protection"
grep -Fq 'item.m_customData[InstancePocketKey] = PocketedValue' "$protection"
grep -Fq 'GetProtectionScope' "$protection"
grep -Fq 'scope={PocketItems.GetProtectionScope(item)}' "$controller"
grep -Fq 'ManualColor' "$marker"
if grep -Fq 'AutomaticColor' "$marker"; then
  printf 'automatic protection must not have a visible marker\n' >&2
  exit 1
fi
grep -Fq 'manuallyProtected && !automaticallyProtected' "$marker"
grep -Fq 'rect.anchorMin = new Vector2(0f, 1f)' "$marker"
grep -Fq 'TextAlignmentOptions.TopLeft' "$marker"
grep -Fq 'if (inventoryWasOpen)' "$quick_stack_feedback"
grep -Fq 'QuickStackMessages.AbovePlayerSummary(movedItems)' "$quick_stack_feedback"
! grep -Fq 'ShowDestinationSummaries' "$quick_stack_feedback"
! grep -Fq 'ShowDestinationSummaries' "$quick_stack"
grep -Fq 'FormatItemsForContainer' "$quick_stack_summary"
grep -Fq 'MessageHud.MessageType.Center' "$quick_stack_feedback"
grep -Fq 'QuickStackReceiptHud.Show(message)' "$quick_stack_feedback"
grep -Fq 'Object.Instantiate(template, template.transform.parent)' "$quick_stack_receipt_hud"
grep -Fq 'ElementGameObjectField' "$quick_stack_receipt_hud"
grep -Fq 'rect.GetWorldCorners' "$quick_stack_receipt_hud"
grep -Fq 'rect.pivot = new Vector2(0f, 1f)' "$quick_stack_receipt_hud"
grep -Fq 'TextAlignmentOptions.TopLeft' "$quick_stack_receipt_hud"
grep -Fq 'QuickStackReceiptHud.Update();' "$root/src/Plugin.cs"
grep -Fq 'QuickStackReceiptHud.Destroy();' "$root/src/Plugin.cs"
grep -Fq 'QuickStackLocation.Format(operation.Player, container)' "$quick_stack"
grep -Fq 'QuickStackDiagnostics.ItemMoved' "$quick_stack"
grep -Fq 'QuickStackAvailability.CanRun' "$quick_stack"
grep -Fq 'InventoryTransactions.TryBeginDeposit' "$quick_stack"
grep -Fq 'InventoryTransactions.IsAvailable' "$quick_stack_availability"
grep -Fq 'matching Benheim versions on the server and every player' "$protocol_core"
grep -Fq 'source.RemoveItem(sourceItem, sourceItem.m_stack)' "$protocol_client"
grep -Fq 'new ZPackage(pending.RequestBytes)' "$protocol_client"
grep -Fq 'ZRoutedRpc.instance.InvokeRoutedRPC(owner, OwnerExecuteRpc' "$protocol_server"
grep -Fq 'InventoryTransactionJournal.WritePrepared' "$protocol_client"
grep -Fq 'InventoryTransactionJournal.MarkReserved' "$protocol_client"
grep -Fq 'InventoryTransactionJournal.MarkCompleted' "$protocol_client"
grep -Fq 'Game.instance.SavePlayerProfile' "$protocol_client"
grep -Fq 'RestoreMissingPreparedItems' "$protocol_root/InventoryTransactionRecovery.cs"
grep -Fq 'InventoryTransactionReceipts.TryRead' "$protocol_owner"
grep -Fq 'InventoryTransactionReceipts.Record' "$protocol_owner"
grep -Fq 'CheckAccessMethod.Invoke' "$protocol_owner"
grep -Fq 'MaxDistance * MaxDistance' "$protocol_owner"
grep -Fq 'namesPresentBefore.Contains' "$protocol_owner"
grep -Fq 'deposit_receipts' "$protocol_receipts"
if rg -F 'ClaimOwnership' "$protocol_root" "$quick_stack"; then
  printf 'authoritative Put Away must never claim chest ownership\n' >&2
  exit 1
fi
grep -Fq 'position=(' "$quick_stack_diagnostics"
grep -Fq 'CompassDirections' "$quick_stack_location"
grep -Fq 'm_animator' "$visibility"
if grep -Fq 'WorldFeedback' "$controller"; then
  printf 'pocket toggles must not show floating world text\n' >&2
  exit 1
fi

printf 'inventory protection marker and local-feedback checks passed\n'
