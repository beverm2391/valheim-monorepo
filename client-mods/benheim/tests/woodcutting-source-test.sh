#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
progression="$root/src/Woodcutting/WoodcuttingProgression.cs"
patches="$root/src/Woodcutting/WoodcuttingPatches.cs"

grep -Fq 'typeof(TreeBase)' "$patches"
grep -Fq 'typeof(TreeLog)' "$patches"

if grep -Eq 'DropTable|m_drop|Instantiate' "$progression"; then
  printf 'woodcutting cleave must not create or modify drops\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/woodcutting/WoodcuttingTests.csproj"

printf 'woodcutting cleave source and behavior checks passed\n'
