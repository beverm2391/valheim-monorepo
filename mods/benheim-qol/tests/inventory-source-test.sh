#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
input_state="$root/src/Infrastructure/InputState.cs"
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
protocol_audit="$protocol_root/InventoryTransactionAudit.cs"
protocol_capabilities="$protocol_root/InventoryTransactionCapabilities.cs"
protocol_models="$protocol_root/InventoryCapabilityModels.cs"
protocol_recovery_policy="$protocol_root/InventoryTransactionRecoveryPolicy.cs"
compatibility_feedback="$root/src/Inventory/MultiplayerCompatibilityFeedback.cs"
client_plugin="$root/src/Plugin.cs"
server_plugin="$root/../../server-mods/benheim-inventory/src/Plugin.cs"
server_project="$root/../../server-mods/benheim-inventory/src/BenheimInventory.csproj"

grep -Fq 'internal static bool IsTextEntryActive()' "$input_state"
grep -Fq 'Minimap.InTextInput()' "$input_state"
grep -Fq 'textInput.m_panel.activeInHierarchy' "$input_state"
grep -Fq 'EventSystem.current?.currentSelectedGameObject' "$input_state"
grep -Fq 'GetComponentInParent<TMP_InputField>()' "$input_state"
test "$(grep -Fc 'if (IsTextEntryActive())' "$input_state")" -eq 3
grep -Fq 'InputState.IsShiftHeld()' "$root/src/Inventory/QuickStackHotkey.cs"
grep -Fq 'InputState.IsKeyDown(KeyCode.P)' "$root/src/Inventory/QuickStackHotkey.cs"
grep -Fq 'InputState.IsTextEntryActive()' "$root/src/Inventory/InventoryPatches.cs"
grep -Fq 'The split dialog is the text-entry surface' "$root/src/Inventory/SplitStackPatches.cs"

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
grep -Fq 'GetComponentsInChildren<TMP_Text>(includeInactive: true)' "$marker"
grep -Fq 'TMP_Settings.defaultFontAsset' "$marker"
if grep -Fq 'fontMaterial' "$marker"; then
  printf 'pocket marker must use the selected font asset material\n' >&2
  exit 1
fi
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
grep -Fq 'internal const int ProtocolVersion = InventoryTransactionRecoveryPolicy.CurrentProtocolVersion;' "$protocol_core"
grep -Fq 'internal const int CurrentProtocolVersion = 2;' "$protocol_recovery_policy"
grep -Fq 'internal const int LegacyJournalProtocolVersion = 1;' "$protocol_recovery_policy"
grep -Fq 'Benheim.Inventory.v2.Hello' "$protocol_core"
grep -Fq 'matching Benheim protocols on the server and every player' "$protocol_core"
grep -Fq 'PluginVersion = "0.1.39"' "$client_plugin"
grep -Fq 'PluginVersion = "0.1.2"' "$server_plugin"
grep -Fq 'Benheim Inventory 0.1.2 loaded with protocol 2.' "$server_plugin"
grep -Fq '<Version>0.1.2</Version>' "$server_project"
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
grep -Fq 'BenheimInventoryAudit.log' "$protocol_audit"
grep -Fq 'InventoryTransactionAudit.Write' "$protocol_core"
grep -Fq 'if (changed)' "$protocol_capabilities"
grep -Fq 'reason=status_stale' "$protocol_capabilities"
grep -Fq 'status.Write(player.IsDetected)' "$protocol_capabilities"
grep -Fq 'isCompatible != (isDetected && playerProtocol == protocol)' "$protocol_capabilities"
grep -Fq 'Semantic versions deliberately do not participate in this key.' "$protocol_models"
grep -Fq 'InventoryCompatibilityWarningTracker' "$compatibility_feedback"
grep -Fq 'Press F8 for details.' "$compatibility_feedback"
if rg -n 'ClientVersion\s*==|ServerVersion\s*==' "$protocol_root"; then
  printf 'semantic versions must remain diagnostic rather than capability gates\n' >&2
  exit 1
fi
grep -Fq 'LogServerBlock' "$protocol_server"
if rg -F -g '*.cs' 'ClaimOwnership' "$protocol_root" "$quick_stack"; then
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
