#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fill="$root/src/Production/StationFill.cs"
remote_batch="$root/src/Production/RemoteSmelterBatch.cs"
batch_rules="$root/src/Production/StationFillBatchRules.cs"
patches="$root/src/Production/StationFillPatches.cs"
overlay="$root/src/Shortcuts/ShortcutOverlayCatalog.cs"

grep -Fq 'InputState.IsShiftHeld()' "$fill"
grep -Fq 'RemoteSmelterBatch.SelectFirstMaterial' "$fill"
grep -Fq 'RemoteSmelterBatch.ShouldUse' "$fill"
grep -Fq 'view.InvokeRPC(RequestRpc, input, package)' "$remote_batch"
grep -Fq 'view.InvokeRPC(requester, ResultRpc, input, accepted)' "$remote_batch"
grep -Fq 'Pending.ContainsKey(key)' "$remote_batch"
grep -Fq 'inventory.RemoveItem(material.m_shared.m_name, requested)' "$remote_batch"
grep -Fq 'StationFillBatchRules.FirstAvailableIndex(counts)' "$remote_batch"
grep -Fq 'StationFillBatchRules.AcceptedCount' "$remote_batch"
grep -Fq 'station_fill_requested' "$remote_batch"
grep -Fq 'station_fill_owner_result' "$remote_batch"
grep -Fq 'accepted={accepted} refunded={returned} dropped={dropped}' "$remote_batch"
grep -Fq 'ItemDrop.DropItem(' "$remote_batch"
if grep -Eq 'ClaimOwnership|retry|journal|transaction|correlation' "$remote_batch"; then
    printf 'remote station fill must remain one ephemeral owner-authoritative request\n' >&2
    exit 1
fi
grep -Fq 'StateUpdateTimeoutSeconds = 1f' "$fill"
grep -Fq 'CreateAddOne' "$fill"
grep -Fq 'user.GetInventory().GetItem(selectedItemName)' "$fill"
grep -Fq 'CreateCookingAddOne' "$fill"
grep -Fq 'CookingHaveDoneItem.Invoke(station, null)' "$fill"
grep -Fq 'direct ready-food interaction entirely native' "$fill"
grep -Fq 'awards the native Cooking skill gain' "$fill"
if grep -Fq 'CookingFindCookableItem' "$fill"; then
    printf 'station fill must preserve the native null-item cooking path\n' >&2
    exit 1
fi
grep -Fq 'station_fill_started' "$fill"
grep -Fq 'station_fill_finished' "$fill"
grep -Fq 'shield_generator_fuel' "$fill"
grep -Fq 'result = "state_update_timeout"' "$fill"
grep -Fq 'result = "station_destroyed"' "$fill"
grep -Fq 'attempted++' "$fill"
grep -Fq 'confirmed++' "$fill"
grep -Fq 'attempted={attempted} confirmed={confirmed}' "$fill"
grep -Fq 'Filled {confirmed} items' "$fill"
grep -Fq 'Utils.GetPrefabName' "$fill"
grep -Fq 'owner_id=' "$fill"
grep -Fq 'data_revision=' "$fill"

grep -Fq 'HarmonyPatch(typeof(Smelter), "OnAddOre")' "$patches"
grep -Fq 'HarmonyPatch(typeof(Smelter), "OnAddFuel")' "$patches"
grep -Fq 'HarmonyPatch(typeof(Smelter), "Awake")' "$patches"
grep -Fq 'HarmonyPatch(typeof(ShieldGenerator), "OnAddFuel")' "$patches"
grep -Fq 'HarmonyPatch(typeof(CookingStation), "OnAddFoodSwitch")' "$patches"
grep -Fq 'HarmonyPatch(typeof(CookingStation), "OnAddFuelSwitch")' "$patches"
grep -Fq 'StationFill.IsInvokingVanilla' "$patches"
grep -Fq 'Left Shift + station input' "$overlay"

dotnet run --project "$root/tests/station-fill/StationFillTests.csproj"

printf 'station fill source checks passed\n'
