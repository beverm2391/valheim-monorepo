#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source="$root/src/Inventory/LoadoutSwap.cs"

grep -Fq '[HarmonyPatch(typeof(ZInput), nameof(ZInput.GetButtonDown), new[] { typeof(string) })]' "$source"
grep -Fq 'InputState.IsModifierHeld()' "$source"
grep -Fq '!InputState.IsKeyDown(KeyCode.R)' "$source"
grep -Fq 'Player.TakeInput has' "$source"
grep -Fq 'InputState.IsTextEntryActive()' "$source"
grep -Fq 'player.EquipItem(first)' "$source"
grep -Fq 'player.EquipItem(second)' "$source"
grep -Fq 'player.EquipItem(solo)' "$source"
grep -Fq 'player.UnequipItem(first)' "$source"
grep -Fq 'player.UnequipItem(second)' "$source"
grep -Fq 'player.UnequipItem(solo)' "$source"
grep -Fq 'rejection = "pair_incompatible"' "$source"
grep -Fq 'IsHandSlot(firstSlot) && IsHandSlot(secondSlot)' "$source"
grep -Fq 'first == ItemDrop.ItemData.ItemType.Torch' "$source"
grep -Fq 'second == ItemDrop.ItemData.ItemType.Shield' "$source"
grep -Fq 'return true;' "$source"
grep -Fq '"swapped",' "$source"

if rg -n 'm_(left|right|hiddenLeft|hiddenRight)Item\s*=' "$source"; then
  printf 'loadout swap must use native equipment methods, not equipment fields\n' >&2
  exit 1
fi

printf 'loadout swap source checks passed\n'
