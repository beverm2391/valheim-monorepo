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

if ! grep -Fq 'CurrentVersion { get; } = new GameVersion(1, 0, 20);' "$native_version"; then
  printf 'Valheim source version mismatch: this native integration check is qualified for 1.0.20\n' >&2
  exit 1
fi

assert_native() {
  local source_file="$1"
  local expected="$2"
  if ! grep -Fq "$expected" "$source_file"; then
    printf 'Valheim 1.0.20 native contract changed: expected "%s" in %s\n' \
      "$expected" "${source_file##*/}" >&2
    exit 1
  fi
}

# Every ItemDrop gets Valheim's own Floating component before native Awake
# caches that component. Native Floating applies forces only for the ZDO owner
# and accepts both water and tar liquid levels.
assert_native "$native_item_drop" 'private void Awake()'
assert_native "$native_item_drop" 'm_floating = GetComponent<Floating>();'
grep -Fq '[HarmonyPatch(typeof(ItemDrop), "Awake")]' "$patches"
grep -Fq '[HarmonyPrefix]' "$patches"
grep -Fq 'LiquidItemRecovery.EnsureNativeBuoyancy(__instance)' "$patches"
grep -Fq 'item.gameObject.AddComponent<Floating>()' "$policy"
grep -Fq 'ObjectDB.instance?.GetItemPrefab(NativeBuoyancyDonor)' "$policy"
assert_native "$native_floating" 'if (!m_body || !m_nview.IsValid() || !m_nview.IsOwner())'
assert_native "$native_floating" 'if (type == LiquidType.Water || type == LiquidType.Tar)'
assert_native "$native_floating" 'm_body.AddForceAtPosition(vector * m_body.mass, worldCenterOfMass, ForceMode.Impulse);'

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
assert_native "$native_item_drop" 'return m_floating.IsInTar();'
assert_native "$native_item_drop" 'Floating.GetLiquidLevel(worldCenterOfMass, 1f, LiquidType.Tar)'
assert_native "$native_floating" 'public bool IsInTar()'

# Native Tar starts as a Pickable. Benheim bypasses only its configured tar
# check and leaves the normal RPC, drop, and effect path in place.
assert_native "$native_pickable" 'public bool Interact(Humanoid character, bool repeat, bool alt)'
assert_native "$native_pickable" 'if (m_tarPreventsPicking)'
assert_native "$native_pickable" 'm_floating.IsInTar()'
assert_native "$native_pickable" 'm_nview.InvokeRPC("RPC_Pick", num);'

# Manual pickup keeps Valheim's interaction range, ownership checks,
# inventory-capacity failure, effects, and messages. Benheim replaces only the
# ItemDrop.InTar call in ItemDrop.Interact.
assert_native "$native_player" 'public float m_maxInteractDistance = 5f;'
assert_native "$native_player" 'Vector3.Distance(m_eye.position, raycastHit.point) < m_maxInteractDistance + (componentInParent?.GetHoverOffset() ?? 0f)'
assert_native "$native_player" 'private void Interact(GameObject go, bool hold, bool alt)'
assert_native "$native_player" 'componentInParent.Interact(this, hold, alt)'
assert_native "$native_item_drop" 'public bool Interact(Humanoid character, bool repeat, bool alt)'
assert_native "$native_item_drop" 'if (InTar())'
assert_native "$native_item_drop" 'Pickup(character);'
assert_native "$native_item_drop" 'if (m_nview.IsValid())'
assert_native "$native_item_drop" 'if (CanPickup())'
assert_native "$native_item_drop" 'RequestOwn();'
assert_native "$native_humanoid" 'bool flag = m_inventory.AddItem(component.m_itemData);'
assert_native "$native_humanoid" 'Message(MessageHud.MessageType.Center, "$msg_noroom");'
assert_native "$native_humanoid" 'm_pickupEffects.Create(base.transform.position, Quaternion.identity, null, 1f, -1, GetZDOID());'

# Auto-pickup keeps Valheim's ownership request, capacity and carry-weight
# checks, range check, item movement, and final Pickup call. Benheim replaces
# only its InTar call.
assert_native "$native_player" 'public float m_autoPickupRange = 2f;'
assert_native "$native_player" 'private void AutoPickup(float dt)'
assert_native "$native_player" 'component.RequestOwn();'
assert_native "$native_player" 'if (component.InTar())'
assert_native "$native_player" '!m_inventory.CanAddItem(component.m_itemData)'
assert_native "$native_player" 'component.m_itemData.GetWeight() + m_inventory.GetTotalWeight() > GetMaxCarryWeight()'
assert_native "$native_player" 'if (num2 > m_autoPickupRange)'
assert_native "$native_player" 'component.transform.position += vector3;'
assert_native "$native_player" 'Pickup(component.gameObject);'

# Other callers still use Valheim's ItemDrop.InTar behavior, including its
# tar-specific protection from timed destruction. The implementation handles
# every item the same way. It does not scan for items, move them in a loop,
# modify inventory directly, or patch liquid behavior.
assert_native "$native_item_drop" 'private void TimedDestruction()'
assert_native "$native_item_drop" '!InTar() && !IsPiece()'
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

printf 'Liquid item recovery source checks passed\n'
