#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
input_state="$root/src/Infrastructure/InputState.cs"
feedback="$root/src/Infrastructure/WorldFeedback.cs"
marker="$root/src/Inventory/PocketMarker.cs"
controller="$root/src/Inventory/PocketItemController.cs"
protection="$root/src/Inventory/PocketItems.cs"
quick_stack="$root/src/Inventory/QuickStack.cs"
quick_stack_diagnostics="$root/src/Inventory/QuickStackDiagnostics.cs"
quick_stack_location="$root/src/Inventory/QuickStackLocation.cs"
quick_stack_feedback="$root/src/Inventory/QuickStackFeedback.cs"
quick_stack_summary="$root/src/Inventory/QuickStackSummary.cs"
quick_stack_receipt_hud="$root/src/Inventory/QuickStackReceiptHud.cs"
visibility="$root/src/Inventory/InventoryVisibility.cs"
client_plugin="$root/src/Plugin.cs"

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
grep -Fq 'container.StackAll();' "$quick_stack"
grep -Fq 'BeginBulkStack' "$quick_stack"
grep -Fq 'AccountsForPutAway' "$quick_stack"
grep -Fq 'RecordNativeTransfer' "$quick_stack"
grep -Fq 'scope.Source.ContainsItem(snapshot.Item)' "$quick_stack"
if rg -F 'InventoryTransactions' "$quick_stack" "$client_plugin"; then
  printf 'client Put Away must use Valheim native ownership rather than InventoryTransactions\n' >&2
  exit 1
fi
grep -Fq 'PluginVersion = "0.1.44"' "$client_plugin"
if rg -n 'InventoryTransaction|InventoryCapability|BenheimInventoryProtocol' "$root/src"; then
  printf 'client Put Away must not retain protocol machinery\n' >&2
  exit 1
fi
if rg -F -g '*.cs' 'ClaimOwnership' "$quick_stack"; then
  printf 'native Put Away must never claim chest ownership\n' >&2
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
