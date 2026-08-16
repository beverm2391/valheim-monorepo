#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
input_state="$root/src/Infrastructure/InputState.cs"
feedback="$root/src/Infrastructure/WorldFeedback.cs"
marker="$root/src/Inventory/PocketMarker.cs"
controller="$root/src/Inventory/PocketItemController.cs"
protection="$root/src/Inventory/PocketItems.cs"
quick_stack="$root/src/Inventory/QuickStack.cs"
quick_stack_transfer="$root/src/Inventory/QuickStackTransfer.cs"
quick_stack_diagnostics="$root/src/Inventory/QuickStackDiagnostics.cs"
quick_stack_location="$root/src/Inventory/QuickStackLocation.cs"
quick_stack_feedback="$root/src/Inventory/QuickStackFeedback.cs"
quick_stack_summary="$root/src/Inventory/QuickStackSummary.cs"
put_away_lease="$root/src/Inventory/PutAwayLeaseClient.cs"
container_write="$root/src/Inventory/QuickStackContainerWrite.cs"
top_left_feedback_hud="$root/src/TopLeftFeedbackHud.cs"
top_left_feedback_layout="$root/src/TopLeftFeedbackLayout.cs"
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
grep -Fq 'MessageHud.MessageType.Center' "$quick_stack_feedback"
grep -Fq 'TopLeftFeedbackHud.ShowGrouped(message)' "$quick_stack_feedback"
grep -Fq 'TopLeftFeedbackHud.ShowTransient(message)' "$controller"
grep -Fq 'TopLeftFeedbackHud.ShowTransient("Put Away already in progress")' "$quick_stack"
grep -Fq '"Put Away busy — retry in a few seconds"' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.TryRequest' "$quick_stack"
grep -Fq 'PutAwayLeaseClient.Release("batch_finished")' "$quick_stack"
grep -Fq 'quick_stack_lease_result' "$put_away_lease"
grep -Fq 'Object.Instantiate(template, parent)' "$top_left_feedback_hud"
grep -Fq 'text.canvasRenderer.SetAlpha(1f)' "$top_left_feedback_hud"
grep -Fq 'text.canvasRenderer.GetAlpha() > VisibleAlphaThreshold' "$top_left_feedback_hud"
grep -Fq 'text.gameObject.activeInHierarchy' "$top_left_feedback_hud"
grep -Fq 'internal static TopLeftFeedbackResult ShowTransient' "$top_left_feedback_hud"
grep -Fq 'TopLeftFeedbackResult.Unavailable' "$top_left_feedback_hud"
grep -Fq 'TopLeftFeedbackResult.CreatedNotPlaced' "$top_left_feedback_hud"
grep -Fq 'TopLeftFeedbackResult.Placed' "$top_left_feedback_hud"
grep -Fq 'Entry' "$top_left_feedback_hud"
grep -Fq 'Entries.Add' "$top_left_feedback_hud"
grep -Fq 'GroupedDurationSeconds = 5f' "$top_left_feedback_hud"
grep -Fq 'TransientDurationSeconds = 4f' "$top_left_feedback_hud"
grep -Fq 'entry.HideAt - now' "$top_left_feedback_hud"
grep -Fq 'entry.Text.alpha = Mathf.Clamp01(remaining / entry.FadeSeconds)' "$top_left_feedback_hud"
grep -Fq 'FindVisibleHotbarBounds' "$top_left_feedback_hud"
grep -Fq 'FindVisibleNativeStatusBounds' "$top_left_feedback_hud"
grep -Fq 'm_statusEffectListRoot' "$top_left_feedback_hud"
grep -Fq 'statusRoot.childCount' "$top_left_feedback_hud"
if grep -Fq 'GetComponentsInChildren' "$top_left_feedback_hud"; then
  printf 'visible feedback placement must not allocate a status-child array each frame\n' >&2
  exit 1
fi
grep -Fq 'Mathf.Max(rect.rect.width, text.preferredWidth)' "$top_left_feedback_hud"
grep -Fq 'TopLeftFeedbackLayout.Calculate' "$top_left_feedback_hud"
grep -Fq 'ToLayoutRect(hotbarBounds.Value)' "$top_left_feedback_hud"
grep -Fq 'nativeStatusBounds.Value' "$top_left_feedback_hud"
grep -Fq 'hotbarRect.GetWorldCorners' "$top_left_feedback_hud"
grep -Fq 'WorldCorners[2]' "$top_left_feedback_hud"
grep -Fq 'rect.pivot = new Vector2(0f, 1f)' "$top_left_feedback_hud"
grep -Fq 'TextAlignmentOptions.TopLeft' "$top_left_feedback_hud"
grep -Fq 'TopLeftFeedbackHud.Update();' "$root/src/Plugin.cs"
grep -Fq 'TopLeftFeedbackHud.Destroy();' "$root/src/Plugin.cs"
grep -Fq 'internal static TopLeftFeedbackPlacement Calculate' "$top_left_feedback_layout"
layout_block="$(sed -n '/internal static TopLeftFeedbackPlacement Calculate/,/private static float Clamp/p' "$top_left_feedback_layout")"
printf '%s\n' "$layout_block" | grep -Fq 'hotbarBounds.XMin'
printf '%s\n' "$layout_block" | grep -Fq 'hotbarBounds.YMin - gap'
printf '%s\n' "$layout_block" | grep -Fq 'nativeStatusBounds.YMin - gap'
if printf '%s\n' "$layout_block" | grep -Fq 'hotbarBounds.XMax'; then
  printf 'Benheim top-left lane must never anchor to the hotbar right edge\n' >&2
  exit 1
fi
grep -Fq 'scaleFactor' "$top_left_feedback_layout"
grep -Fq 'screenHeight - fallbackTopOffset * scaleFactor' "$top_left_feedback_layout"
if grep -Fq 'MessageHud.MessageType.TopLeft' "$controller" "$quick_stack"; then
  printf 'Benheim-owned top-left feedback must use the shared lane\n' >&2
  exit 1
fi
grep -Fq 'QuickStackLocation.Format(operation.Player, container)' "$quick_stack_transfer"
grep -Fq 'QuickStackDiagnostics.ItemMoved' "$quick_stack_transfer"
grep -Fq 'container.StackAll();' "$quick_stack"
grep -Fq 'QuickStackContainerWrite.TryBegin' "$quick_stack"
grep -Fq 'networkView.ClaimOwnership();' "$container_write"
grep -Fq 'if (!ownerAfter)' "$container_write"
grep -Fq '"revision_advanced"' "$quick_stack_diagnostics"
grep -Fq 'scope.ContainerWrite?.Complete(movedItems);' "$quick_stack"
grep -Fq 'BeginBulkStack' "$quick_stack"
grep -Fq 'RecordNativeTransfer' "$quick_stack"
grep -Fq 'scope.Player.GetInventory().ContainsItem(snapshot.Item)' "$quick_stack_transfer"
if rg -F 'InventoryTransactions' "$quick_stack" "$client_plugin"; then
  printf 'client Put Away must use Valheim native ownership rather than InventoryTransactions\n' >&2
  exit 1
fi
grep -Fq 'PluginVersion = "0.1.61"' "$client_plugin"
if rg -n 'InventoryTransaction|InventoryCapability|BenheimInventoryProtocol|CorrelatedStack' "$root/src"; then
  printf 'client Put Away must not retain protocol machinery\n' >&2
  exit 1
fi
claim_count="$(rg -F -g '*.cs' 'ClaimOwnership' "$root/src/Inventory" | wc -l | tr -d ' ')"
if [[ "$claim_count" != "1" ]]; then
  printf 'Put Away ownership claim must stay at the single post-grant write boundary\n' >&2
  exit 1
fi
grep -Fq '"position"' "$quick_stack_diagnostics"
grep -Fq '"container_open_snapshot"' "$quick_stack_diagnostics"
grep -Fq '"contents"' "$quick_stack_diagnostics"
grep -Fq 'OpenedChests.Add(zdoId)' "$quick_stack_diagnostics"
grep -Fq 'CompassDirections' "$quick_stack_location"
grep -Fq 'm_animator' "$visibility"
if grep -Fq 'WorldFeedback' "$controller"; then
  printf 'pocket toggles must not show floating world text\n' >&2
  exit 1
fi

printf 'inventory protection marker and local-feedback checks passed\n'
