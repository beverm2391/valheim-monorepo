#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
policy="$root/src/Interaction/TarCollectibleInteraction.cs"
patches="$root/src/Interaction/TarCollectiblePatches.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_version="$source_tree/Version.cs"
native_pickable="$source_tree/Pickable.cs"
native_item_drop="$source_tree/ItemDrop.cs"
native_player="$source_tree/Player.cs"

grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$native_version"

# Installed assets identify two native pit pickables that both spawn the
# ordinary Tar ItemDrop. Only Pickable_Tar enables Pickable's tar gate;
# Pickable_TarBig already follows the native pick path while submerged.
grep -Fq 'SmallTarPickablePrefab = "Pickable_Tar"' "$policy"
grep -Fq 'BigTarPickablePrefab = "Pickable_TarBig"' "$policy"
grep -Fq 'TarItemPrefab = "Tar"' "$policy"
grep -Fq 'TarItemName = "$item_tar"' "$policy"
grep -Fq 'pickable.m_itemPrefab == null' "$policy"
grep -Fq 'itemDrop.m_itemData.m_dropPrefab != null' "$policy"
grep -Fq 'itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Material' "$policy"

# Each transpiler replaces exactly the one early manual-interaction gate and
# leaves Valheim's normal RPC and Pickup tails intact.
grep -Fq 'if (m_tarPreventsPicking)' "$native_pickable"
grep -Fq 'm_nview.InvokeRPC("RPC_Pick", num);' "$native_pickable"
grep -Fq 'if (InTar())' "$native_item_drop"
grep -Fq 'Pickup(character);' "$native_item_drop"
grep -Fq 'Expected one Pickable tar interaction gate' "$patches"
grep -Fq 'Expected one ItemDrop tar interaction gate' "$patches"
grep -Fq 'TarCollectibleInteraction.ShouldBlockPickable' "$patches"
grep -Fq 'TarCollectibleInteraction.ShouldBlockItemDrop' "$patches"

# Auto-pickup remains a separate native path and still rejects every ItemDrop
# that reports itself in tar. Benheim patches neither Player nor InTar.
grep -Fq 'if (component.InTar())' "$native_player"
if grep -Eq 'HarmonyPatch\(typeof\(Player\).*AutoPickup|HarmonyPatch\(typeof\(ItemDrop\).*InTar' "$patches"; then
  printf 'Tar collection must not patch auto-pickup or ItemDrop.InTar globally\n' >&2
  exit 1
fi

grep -Fq 'tar_collectible_interaction' "$policy"
grep -Fq '.Boolean("exemption_applied", observation.ExemptionApplied)' "$policy"
grep -Fq '.Boolean("native_result", nativeResult)' "$policy"

dotnet run --project "$root/tests/native-mechanic-transpilers/NativeMechanicTranspilerTests.csproj"

printf 'Tar collectible interaction source checks passed\n'
