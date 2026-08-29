#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_tree_log="$source_tree/TreeLog.cs"
source "$root/scripts/valheim-source-lib.sh"
valheim_source_resolve_assembly || {
  printf 'finewood drops: %s\n' "$VALHEIM_SOURCE_ERROR" >&2
  exit 1
}
data_dir="$(cd "$(dirname "$VALHEIM_SOURCE_ASSEMBLY_PATH")/.." && pwd -P)"
softref_manifest="$data_dir/StreamingAssets/SoftRef/manifest_extended"

rpc_damage="$(sed -n '/private void RPC_Damage(long sender, HitData hit)/,/^}/p' "$native_tree_log")"
destroy="$(sed -n '/private void Destroy(HitData hitData = null)/,/^}/p' "$native_tree_log")"

# Installed Valheim 0.221.12 sends damage to the current ZDO owner. Only that
# owner enters Destroy, realizes the native drop list, and performs spawning.
[[ "$(grep -Fc 'if (!m_nview.IsOwner())' <<<"$rpc_damage")" -eq 1 ]]
[[ "$(grep -Fc 'Destroy(hitData);' <<<"$rpc_damage")" -eq 1 ]]
[[ "$(grep -Fc 'Destroy(hitData);' "$native_tree_log")" -eq 1 ]]
owner_guard_line="$(grep -nF 'if (!m_nview.IsOwner())' <<<"$rpc_damage" | cut -d: -f1)"
owner_return_line="$(grep -nF 'return;' <<<"$rpc_damage" | head -n 1 | cut -d: -f1)"
destroy_call_line="$(grep -nF 'Destroy(hitData);' <<<"$rpc_damage" | cut -d: -f1)"
((owner_guard_line < owner_return_line))
((owner_return_line < destroy_call_line))

[[ "$(grep -Fc 'List<GameObject> dropList = m_dropWhenDestroyed.GetDropList();' <<<"$destroy")" -eq 1 ]]
[[ "$(grep -Fc 'gameObject = Game.instance.CheckDropConversion(hitData, component, gameObject, ref dropCount);' <<<"$destroy")" -eq 1 ]]
[[ "$(grep -Fc 'ItemDrop.OnCreateNew(UnityEngine.Object.Instantiate(gameObject, position, rotation));' <<<"$destroy")" -eq 1 ]]
drop_list_line="$(grep -nF 'List<GameObject> dropList = m_dropWhenDestroyed.GetDropList();' <<<"$destroy" | cut -d: -f1)"
native_conversion_line="$(grep -nF 'gameObject = Game.instance.CheckDropConversion(hitData, component, gameObject, ref dropCount);' <<<"$destroy" | cut -d: -f1)"
native_spawn_line="$(grep -nF 'ItemDrop.OnCreateNew(UnityEngine.Object.Instantiate(gameObject, position, rotation));' <<<"$destroy" | cut -d: -f1)"
((drop_list_line < native_conversion_line))
((native_conversion_line < native_spawn_line))

# The allowlist is anchored to the exact native prefab identities installed
# with the assembly under test instead of only repeating production constants.
for asset_path in \
  'Assets/world/Props/Birch/logs/Birch_log.prefab' \
  'Assets/world/Props/Birch/logs/Birch_log_half.prefab' \
  'Assets/world/Props/oak/logs/Oak_log.prefab' \
  'Assets/world/Props/oak/logs/Oak_log_half.prefab' \
  'Assets/GameElements/Items/materials/Wood.prefab' \
  'Assets/GameElements/Items/materials/FineWood.prefab'; do
  [[ "$(strings "$softref_manifest" | grep -Fc "path in bundle: $asset_path")" -eq 1 ]]
done

dotnet run --project "$root/tests/finewood-drops/FinewoodDropTests.csproj"
