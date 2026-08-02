#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
progression="$root/src/Woodcutting/WoodcuttingProgression.cs"
patches="$root/src/Woodcutting/WoodcuttingPatches.cs"

grep -Fq 'CleaveUnlockLevel = 25f' "$progression"
grep -Fq 'MinCleaveChance = 0.15f' "$progression"
grep -Fq 'MaxCleaveChance = 0.45f' "$progression"
grep -Fq 'CleaveDamageMultiplier = 0.5f' "$progression"
grep -Fq 'Skills.SkillType.WoodCutting' "$progression"
grep -Fq 'DamageText.instance?.ShowText' "$progression"
grep -Fq 'DamageText.TextType.Bonus' "$progression"
grep -Fq 'typeof(TreeBase)' "$patches"
grep -Fq 'typeof(TreeLog)' "$patches"

if grep -Eq 'DropTable|m_drop|Instantiate' "$progression"; then
  printf 'woodcutting cleave must not create or modify drops\n' >&2
  exit 1
fi

printf 'woodcutting cleave source checks passed\n'
