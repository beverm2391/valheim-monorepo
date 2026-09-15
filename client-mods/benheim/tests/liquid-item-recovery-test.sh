#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
policy="$root/src/Interaction/LiquidItemRecovery.cs"
patches="$root/src/Interaction/LiquidItemRecoveryPatches.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_version="$source_tree/Version.cs"
native_pickable="$source_tree/Pickable.cs"
native_item_drop="$source_tree/ItemDrop.cs"
native_humanoid="$source_tree/Humanoid.cs"
native_player="$source_tree/Player.cs"
native_floating="$source_tree/Floating.cs"

grep -Fq 'CurrentVersion { get; } = new GameVersion(1, 0, 12);' "$native_version"

# Every ItemDrop gets Valheim's own Floating component before native Awake
# caches that component. Native Floating applies forces only for the ZDO owner
# and accepts both water and tar liquid levels.
grep -Fq 'm_floating = GetComponent<Floating>();' "$native_item_drop"
grep -Fq '[HarmonyPatch(typeof(ItemDrop), "Awake")]' "$patches"
grep -Fq '[HarmonyPrefix]' "$patches"
grep -Fq 'LiquidItemRecovery.EnsureNativeBuoyancy(__instance)' "$patches"
grep -Fq 'item.gameObject.AddComponent<Floating>()' "$policy"
grep -Fq 'ObjectDB.instance?.GetItemPrefab(NativeBuoyancyDonor)' "$policy"
grep -Fq 'if (!m_body || !m_nview.IsValid() || !m_nview.IsOwner())' "$native_floating"
grep -Fq 'if (type == LiquidType.Water || type == LiquidType.Tar)' "$native_floating"
grep -Fq 'm_body.AddForceAtPosition(vector * m_body.mass, worldCenterOfMass, ForceMode.Impulse);' "$native_floating"

# Buoyancy is ephemeral component state. The feature does not replace an item,
# mutate ItemData, write a ZDO, move an inventory, or add custom saved fields.
if grep -Eq 'm_itemData\s*=|m_stack\s*=|GetZDO|ZDOVars|AddItem\(|Pickup\(|Save\(' "$policy" "$patches"; then
  printf 'Liquid item recovery must not mutate native item or persistence data\n' >&2
  exit 1
fi
grep -Fq 'DiagnosticEvent.Create("Interaction", "item_buoyancy_setup")' "$patches"
grep -Fq '.String("mechanism", "native_floating_component")' "$patches"
grep -Fq '.String("liquids", "water_and_tar")' "$patches"

# Valheim already distinguishes tar from other liquids. ItemDrop.InTar checks
# Floating for LiquidType.Tar, so bypassing that result only at pickup calls
# does not affect water or other liquids.
grep -Fq 'return m_floating.IsInTar();' "$native_item_drop"
grep -Fq 'Floating.GetLiquidLevel(worldCenterOfMass, 1f, LiquidType.Tar)' "$native_item_drop"
grep -Fq 'public bool IsInTar()' "$native_floating"

# Native Tar starts as a Pickable. Benheim bypasses only its configured tar
# check and leaves the normal RPC, drop, and effect path in place.
grep -Fq 'if (m_tarPreventsPicking)' "$native_pickable"
grep -Fq 'm_floating.IsInTar()' "$native_pickable"
grep -Fq 'm_nview.InvokeRPC("RPC_Pick", num);' "$native_pickable"

# Manual pickup keeps Valheim's interaction range, ownership checks,
# inventory-capacity failure, effects, and messages. Benheim replaces only the
# ItemDrop.InTar call in ItemDrop.Interact.
grep -Fq 'm_maxInteractDistance = 5f' "$native_player"
grep -Fq 'Vector3.Distance(m_eye.position, raycastHit.point) < m_maxInteractDistance' "$native_player"
grep -Fq 'if (InTar())' "$native_item_drop"
grep -Fq 'Pickup(character);' "$native_item_drop"
grep -Fq 'if (m_nview.IsValid())' "$native_item_drop"
grep -Fq 'if (CanPickup())' "$native_item_drop"
grep -Fq 'RequestOwn();' "$native_item_drop"
grep -Fq 'bool flag = m_inventory.AddItem(component.m_itemData);' "$native_humanoid"
grep -Fq 'Message(MessageHud.MessageType.Center, "$msg_noroom");' "$native_humanoid"
grep -Fq 'm_pickupEffects.Create(base.transform.position, Quaternion.identity, null, 1f, -1, GetZDOID());' "$native_humanoid"

# Auto-pickup keeps Valheim's ownership request, capacity and carry-weight
# checks, range check, item movement, and final Pickup call. Benheim replaces
# only its InTar call.
grep -Fq 'm_autoPickupRange = 2f' "$native_player"
grep -Fq 'component.RequestOwn();' "$native_player"
grep -Fq 'if (component.InTar())' "$native_player"
grep -Fq '!m_inventory.CanAddItem(component.m_itemData)' "$native_player"
grep -Fq 'component.m_itemData.GetWeight() + m_inventory.GetTotalWeight() > GetMaxCarryWeight()' "$native_player"
grep -Fq 'if (num2 > m_autoPickupRange)' "$native_player"
grep -Fq 'component.transform.position += vector3;' "$native_player"
grep -Fq 'Pickup(component.gameObject);' "$native_player"

# Other callers still use Valheim's ItemDrop.InTar behavior, including its
# tar-specific protection from timed destruction. The implementation handles
# every item the same way. It does not scan for items, move them in a loop,
# modify inventory directly, or patch liquid behavior.
grep -Fq 'private void TimedDestruction()' "$native_item_drop"
grep -Fq '!InTar() && !IsPiece()' "$native_item_drop"
grep -Fq 'ShouldBlockPickable(Pickable _) => false;' "$policy"
grep -Fq 'ShouldBlockItemDrop(ItemDrop _) => false;' "$policy"
if grep -Eq 'Pickable_Tar|TarItemPrefab|TarItemName|\$item_tar|\$item_stone|\$item_wood' "$policy"; then
  printf 'Tar-pit pickup must not use an item-specific allowlist\n' >&2
  exit 1
fi
if grep -Eq 'Physics\.Overlap|transform\.position|AddItem\(|Pickup\(' "$policy" "$patches"; then
  printf "Tar-pit pickup must use Valheim's existing pickup methods\n" >&2
  exit 1
fi
if tr '\n' ' ' < "$patches" | grep -Eq 'HarmonyPatch\(typeof\((ItemDrop|Floating|LiquidSurface|LiquidVolume)\),[^]]*InTar'; then
  printf 'Tar-pit pickup must not patch tar classification globally\n' >&2
  exit 1
fi

grep -Fq 'Expected exactly one Pickable tar pickup check' "$patches"
grep -Fq 'Expected exactly one {nativeMethod} tar pickup check' "$patches"
grep -Fq '"ItemDrop.Interact"' "$patches"
grep -Fq '"Player.AutoPickup"' "$patches"
grep -Fq '[HarmonyPatch(typeof(Player), "AutoPickup", new[] { typeof(float) })]' "$patches"
grep -Fq 'LiquidItemRecovery.ShouldBlockItemDrop' "$patches"

dotnet run --project "$root/tests/native-mechanic-transpilers/NativeMechanicTranspilerTests.csproj"

printf 'Liquid item recovery source checks passed\n'
