#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patch="$root/src/Interaction/FeastInteractionRangePatch.cs"
ranges="$root/src/Interaction/InteractionRanges.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native="$source_tree/Feast.cs"

# Installed Valheim 1.0.20 uses the same private distance gate for hover text
# and interaction, before it reaches native food eligibility and owner RPCs.
if ! grep -Fq 'CurrentVersion { get; } = new GameVersion(1, 0, 20);' "$source_tree/Version.cs"; then
  printf 'Valheim source version mismatch: this native integration check is qualified for 1.0.20\n' >&2
  exit 1
fi

assert_native() {
  local expected="$1"
  if ! grep -Fq "$expected" "$native"; then
    printf 'Valheim 1.0.20 Feast contract changed: expected "%s" in Feast.cs\n' \
      "$expected" >&2
    exit 1
  fi
}

assert_native 'private void Start()'
assert_native 'public float m_useDistance = 2f;'
assert_native 'private bool InUseDistance(Humanoid human)'
assert_native 'return Vector3.Distance(human.transform.position, base.transform.position) < m_useDistance;'
assert_native 'if (!InUseDistance(Player.m_localPlayer))'
assert_native 'public bool Interact(Humanoid human, bool hold, bool alt)'
assert_native 'if (!player || !InUseDistance(player))'
assert_native 'if (!player.CanConsumeItem(m_foodItem.m_itemData, checkWorldLevel: true))'
assert_native 'm_nview.InvokeRPC("RPC_TryEat");'
assert_native 'if (!m_nview.IsOwner())'
assert_native 'm_nview.InvokeRPC(ZNetView.Everybody, "RPC_OnEat");'
assert_native 'm_nview.InvokeRPC(sender, "RPC_EatConfirmation");'

# Benheim raises only Feast.m_useDistance after native initialization. The
# shared 8-meter targeting range and every unrelated range remain unchanged.
grep -Fq 'internal const float UseDistance = 8f;' "$ranges"
grep -Fq 'internal const float ContainerAutoCloseDistance = 10f;' "$ranges"
grep -Fq '[HarmonyPatch(typeof(Feast), "Start")]' "$patch"
grep -Fq '__instance.m_useDistance = FeastInteractionRange.Resolve(previous);' "$patch"

if rg -n 'Diagnostics|GetHoverText|Feast\.Interact|\bInUseDistance\b|\bCanConsumeItem\b|\bGetStack\b|m_eatStacks|m_foodItem|m_feastParts|m_eatEffect|ZNet|ZDO|RPC|Player|CraftingStation|InventoryGui|m_maxInteractDistance|m_autoCloseDistance' "$patch"; then
  printf 'Feast range patch must not replace native Feast behavior or alter unrelated interaction ranges\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/feast-interaction-range/FeastInteractionRangeTests.csproj"

printf 'Feast interaction range source checks passed\n'
