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
grep -Fq 'DiagnosticEvent.Create("Cooking", "native_bonus_roll")' "$source_file"
grep -Fq '.String("source", source)' "$source_file"
grep -Fq '.Boolean("cooking_gate", cookingGate)' "$source_file"
grep -Fq '.Number("configured_base_chance", configuredBaseChance)' "$source_file"
grep -Fq '.Number("native_skill_factor", nativeSkillFactor)' "$source_file"
grep -Fq '.Number("effective_chance", effectiveChance)' "$source_file"
grep -Fq '.Number("roll", nativeRoll)' "$source_file"
grep -Fq '.Boolean("succeeded", succeeded)' "$source_file"
grep -Fq '.Integer("bonus_count", bonusCount)' "$source_file"
grep -Fq '"native_result_increment",' "$source_file"
grep -Fq '.Integer("native_result_increment", nativeResultIncrement)' "$source_file"
grep -Fq '"native_result_count",' "$source_file"
grep -Fq 'bool report = cookingGate || !nonCookingGuardReported;' "$source_file"
grep -Fq 'codes.Insert(index + 2, new CodeInstruction(OpCodes.Call, rollObserver));' "$source_file"
grep -Fq '? OpCodes.Brfalse_S' "$source_file"

if rg -n 'm_craftBonusChance\s*=|m_craftBonusAmount\s*=|RaiseSkill|InvokeRPC|RPC_|m_cookTime|m_secPerFuel' "$source_file"; then
  printf 'Cooking bonus must only replace native chance reads at the two Cooking seams\n' >&2
  exit 1
fi

printf 'cooking bonus source checks passed\n'
