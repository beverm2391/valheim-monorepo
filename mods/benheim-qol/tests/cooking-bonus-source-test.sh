#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Production/CookingBonusPatches.cs"
source_tree="$($root/scripts/ensure-valheim-source.sh)"
native_inventory="$source_tree/InventoryGui.cs"
native_station="$source_tree/CookingStation.cs"

grep -Fq 'CurrentVersion { get; } = new GameVersion(0, 221, 12);' "$source_tree/Version.cs"
grep -Fq 'public float m_craftBonusChance = 0.25f;' "$native_inventory"
grep -Fq 'public int m_craftBonusAmount = 1;' "$native_inventory"
grep -Fq 'currentCraftingStation.m_craftingSkill != Skills.SkillType.None' "$native_inventory"
grep -Fq 'UnityEngine.Random.value < skillFactor * m_craftBonusChance' "$native_inventory"
grep -Fq 'UnityEngine.Random.value < skillFactor * InventoryGui.instance.m_craftBonusChance' "$native_station"
grep -Fq 'num += InventoryGui.instance.m_craftBonusAmount;' "$native_station"

grep -Fq 'internal const float ChanceCeiling = 0.50f;' "$source_file"
grep -Fq 'station.m_craftingSkill == Skills.SkillType.Cooking' "$source_file"
grep -Fq ': inventoryGui.m_craftBonusChance;' "$source_file"
grep -Fq '[HarmonyPatch(typeof(InventoryGui), "DoCrafting")]' "$source_file"
grep -Fq '[HarmonyPatch(typeof(CookingStation), "OnInteract")]' "$source_file"
grep -Fq 'if (replaced != 1)' "$source_file"
grep -Fq 'code.opcode = OpCodes.Call;' "$source_file"
grep -Fq 'code.operand = replacement;' "$source_file"

if rg -n 'm_craftBonusChance\s*=|m_craftBonusAmount|RaiseSkill|InvokeRPC|RPC_|m_cookTime|m_secPerFuel' "$source_file"; then
  printf 'Cooking bonus must only replace native chance reads at the two Cooking seams\n' >&2
  exit 1
fi

printf 'cooking bonus source checks passed\n'
