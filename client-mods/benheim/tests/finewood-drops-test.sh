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
softref_manifest_text="$(strings "$softref_manifest")"

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
  'Assets/world/Props/PineTree/logs/PineTree_log.prefab' \
  'Assets/world/Props/PineTree/logs/PineTree_log_half.prefab' \
  'Assets/GameElements/Items/materials/Wood.prefab' \
  'Assets/GameElements/Items/materials/FineWood.prefab' \
  'Assets/GameElements/Items/materials/RoundLog.prefab'; do
  [[ "$(grep -Fc "path in bundle: $asset_path" <<<"$softref_manifest_text")" -eq 1 ]]
done

# Inspect only the serialized Pine contract that the conversion depends on.
# This fails closed on identity, TreeLog component shape, sublog behavior, or
# destruction-drop changes without coupling the proof to unrelated assets in
# the same bundle.
pine_bundle_id="$(awk '
  /^  bundle: / { bundle = $2 }
  /^  path in bundle: Assets\/world\/Props\/PineTree\/logs\/PineTree_log.prefab$/ {
    print bundle
  }
' <<<"$softref_manifest_text")"
pine_bundle="$data_dir/StreamingAssets/SoftRef/Bundles/$pine_bundle_id"
command -v uv >/dev/null || {
  printf 'finewood drops: uv is required for installed Pine prefab inspection\n' >&2
  exit 1
}
uv run --quiet --with 'UnityPy==1.25.3' python - "$pine_bundle" <<'PY'
import sys

import UnityPy

environment = UnityPy.load(sys.argv[1])
full_path = "Assets/world/Props/PineTree/logs/PineTree_log.prefab"
half_path = "Assets/world/Props/PineTree/logs/PineTree_log_half.prefab"


def prefab_contract(path):
    root = environment.container[path]
    root_tree = root.read_typetree()
    tree_logs = []
    for component in root_tree["m_Component"]:
        component_id = component["component"]["m_PathID"]
        component_object = root.assetsfile.objects[component_id]
        if component_object.type.name != "MonoBehaviour":
            continue
        component_tree = component_object.read_typetree()
        if "m_dropWhenDestroyed" in component_tree:
            tree_logs.append(component_tree)
    assert len(tree_logs) == 1
    return root, root_tree["m_Name"], tree_logs[0]


full_root, full_name, full_log = prefab_contract(full_path)
half_root, half_name, half_log = prefab_contract(half_path)
assert full_name == "PineTree_log"
assert half_name == "PineTree_log_half"
assert full_log["m_Script"] == half_log["m_Script"]
assert full_log["m_dropWhenDestroyed"]["m_drops"] == []
assert len(full_log["m_subLogPoints"]) == 2
sublog_id = full_log["m_subLogPrefab"]["m_PathID"]
assert full_root.assetsfile.objects[sublog_id].read_typetree()["m_Name"] == half_name

half_drops = half_log["m_dropWhenDestroyed"]
assert half_drops["m_dropMin"] == 15
assert half_drops["m_dropMax"] == 15
assert half_drops["m_dropChance"] == 1.0
assert half_drops["m_oneOfEach"] == 0
assert len(half_drops["m_drops"]) == 2
items = {}
for drop in half_drops["m_drops"]:
    item_id = drop["m_item"]["m_PathID"]
    item_name = half_root.assetsfile.objects[item_id].read_typetree()["m_Name"]
    items[item_name] = {
        "stack_min": drop["m_stackMin"],
        "stack_max": drop["m_stackMax"],
        "weight": drop["m_weight"],
        "dont_scale": drop["m_dontScale"],
    }
assert items == {
    "Wood": {"stack_min": 1, "stack_max": 1, "weight": 1.0, "dont_scale": 0},
    "RoundLog": {"stack_min": 1, "stack_max": 1, "weight": 1.0, "dont_scale": 0},
}
assert half_log["m_subLogPrefab"]["m_PathID"] == 0
assert half_log["m_subLogPoints"] == []
PY

dotnet run --project "$root/tests/finewood-drops/FinewoodDropTests.csproj"
