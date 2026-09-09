#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
patch="$root/src/Interaction/FeastInteractionRangePatch.cs"
ranges="$root/src/Interaction/InteractionRanges.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native="$source_tree/Feast.cs"

# Installed Valheim 0.221.12 uses the same private distance gate for hover text
# and interaction, before it reaches native food eligibility and owner RPCs.
grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$source_tree/Version.cs"
grep -Fq 'public float m_useDistance = 2f;' "$native"
grep -Fq 'private bool InUseDistance(Humanoid human)' "$native"
grep -Fq 'return Vector3.Distance(human.transform.position, base.transform.position) < m_useDistance;' "$native"
grep -Fq 'if (!InUseDistance(Player.m_localPlayer))' "$native"
grep -Fq 'if (!player || !InUseDistance(player))' "$native"
grep -Fq 'if (!player.CanConsumeItem(m_foodItem.m_itemData, checkWorldLevel: true))' "$native"
grep -Fq 'm_nview.InvokeRPC("RPC_TryEat");' "$native"
grep -Fq 'if (!m_nview.IsOwner())' "$native"
grep -Fq 'm_nview.InvokeRPC(ZNetView.Everybody, "RPC_OnEat");' "$native"
grep -Fq 'm_nview.InvokeRPC(sender, "RPC_EatConfirmation");' "$native"

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
