#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
legacy_client="$root/src/EnemyTiers/BenheimTestCommandClient.cs"

if rg -n 'RuntimePrimitiveCatalogCommand|ComfortDiagnosticCommand|CharacterColliderOverlay|bh debug (catalog|comfort|colliders)' "$legacy_client"; then
  printf 'the legacy bh admin command must not dispatch or advertise migrated diagnostics\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/developer-diagnostics-registry/DeveloperDiagnosticsRegistryTests.csproj"

printf 'developer diagnostics registry source and behavior checks passed\n'
