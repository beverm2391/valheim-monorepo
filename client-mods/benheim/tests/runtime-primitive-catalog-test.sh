#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
request="$root/src/Infrastructure/RuntimePrimitiveCatalogRequest.cs"
catalog="$root/src/Infrastructure/RuntimePrimitiveCatalog.cs"
effect_catalog="$root/src/Infrastructure/RuntimePrimitiveEffectCatalog.cs"
command="$root/src/Infrastructure/RuntimePrimitiveCatalogCommand.cs"
readiness="$root/src/Infrastructure/RuntimePrimitiveCatalogReadiness.cs"
selection="$root/src/Infrastructure/RuntimePrimitiveCatalogSelection.cs"
snapshot_file="$root/src/Infrastructure/RuntimePrimitiveSnapshotFile.cs"
rg -Fq 'RuntimePrimitiveCatalogRequest.TryCreate(' "$command"
rg -Fq 'arguments.Length > 1' "$request"

rg -Fq 'ObjectDB.instance' "$command"
rg -Fq 'worldReady: Player.m_localPlayer != null' "$command"
rg -Fq 'database.m_StatusEffects.Count > 0' "$command"
rg -Fq 'database.m_items.Count > 0' "$command"
rg -Fq 'inventoryGuiReady: InventoryGui.instance != null' "$command"
rg -Fq 'hudReady: Hud.instance != null' "$command"
rg -Fq 'messageHudReady: MessageHud.instance != null' "$command"
rg -Fq 'menuReady: menu != null' "$command"
rg -Fq 'tmpReady: TMP_Settings.defaultFontAsset != null' "$command"
rg -Fq 'if (!availability.WorldReady)' "$readiness"
rg -Fq 'runtime catalog unavailable' "$command"

rg -Fq '"object_db_status_effect"' "$effect_catalog"
rg -Fq 'shared.m_consumeStatusEffect' "$effect_catalog"
rg -Fq 'shared.m_equipStatusEffect' "$effect_catalog"
rg -Fq 'shared.m_setStatusEffect' "$effect_catalog"
rg -Fq 'shared.m_attackStatusEffect' "$effect_catalog"
rg -Fq 'shared.m_perfectBlockStatusEffect' "$effect_catalog"
rg -Fq 'shared.m_fullAdrenalineSE' "$effect_catalog"
rg -Fq '.String("display_name"' "$effect_catalog"
rg -Fq '.Boolean("icon_present"' "$effect_catalog"
rg -Fq '.String("sprite_identity"' "$effect_catalog"
rg -Fq 'RuntimePrimitiveCatalogPolicy.IsNativeRuntimeType(' "$effect_catalog"

rg -Fq 'Resources.FindObjectsOfTypeAll<TMP_FontAsset>()' "$catalog"
rg -Fq 'Resources.FindObjectsOfTypeAll<Material>()' "$catalog"
rg -Fq 'GetComponentsInChildren<TMP_Text>(includeInactive: true)' "$catalog"
rg -Fq '"text_donor"' "$catalog"

rg -Fq 'GetComponentsInChildren<Image>(includeInactive: true)' "$catalog"
rg -Fq 'GetComponentsInChildren<Button>(includeInactive: true)' "$catalog"
rg -Fq 'GetComponentsInChildren<Toggle>(includeInactive: true)' "$catalog"
rg -Fq 'GetComponentsInChildren<ScrollRect>(includeInactive: true)' "$catalog"
rg -Fq 'GetComponentsInChildren<Scrollbar>(includeInactive: true)' "$catalog"
rg -Fq 'current.GetSiblingIndex()' "$catalog"
rg -Fq 'IsPluginOwnedSubtree(root.Transform' "$catalog"
rg -Fq 'fontIdentities.Add(fontIdentity)' "$catalog"

rg -Fq 'BenheimRuntimeCatalog.ndjson' "$command"
rg -Fq 'source_count' "$selection"
rg -Fq 'matched_count' "$selection"
rg -Fq 'written_count' "$selection"
rg -Fq 'MaximumSnapshotEntries' "$command"
rg -Fq 'File.Replace(temporaryPath, path' "$snapshot_file"

if rg -n 'FindObjectsOfTypeAll<Sprite>|Diagnostics\.Emit|RemoteDiagnostics|bh debug' "$catalog" "$command" "$request"; then
  printf 'runtime catalog must remain a bounded local donor snapshot, not sprite spam or remote diagnostics\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/runtime-primitive-catalog/RuntimePrimitiveCatalogTests.csproj"

printf 'runtime primitive catalog source and command checks passed\n'
