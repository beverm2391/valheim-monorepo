#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manager="$root/src/Infrastructure/PatchGroupManager.cs"
plugin="$root/src/Plugin.cs"

grep -Fq 'CreateClassProcessor(patchType).Patch();' "$manager"
grep -Fq 'harmony?.UnpatchSelf();' "$manager"
grep -Fq 'ResolveOwner(patchType)' "$manager"
grep -Fq 'typeof(Plugin).Assembly.GetTypes()' "$plugin"
if grep -Fq '.PatchAll(' "$plugin"; then
  printf 'Benheim must not patch the whole assembly as one Harmony transaction\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/patch-groups/PatchGroupTests.csproj"

printf 'feature patch-group isolation checks passed\n'
