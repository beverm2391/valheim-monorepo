#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
runtime="$root/src/DeveloperDiagnostics/DeveloperDiagnosticsRuntime.cs"
plugin="$root/src/Plugin.cs"
legacy_client="$root/src/EnemyTiers/BenheimTestCommandClient.cs"

rg -Fq '"bhcatalog"' "$runtime"
rg -Fq '"bhrun"' "$runtime"
rg -Fq '"bhwatch"' "$runtime"
rg -Fq 'optionsFetcher: CatalogNames' "$runtime"
rg -Fq 'optionsFetcher: SnapshotNames' "$runtime"
rg -Fq 'optionsFetcher: WatcherNames' "$runtime"
rg -Fq 'ColliderShippedDefault = false' "$runtime"
rg -Fq 'session={colliderSetting.ToString().ToLowerInvariant()}' "$runtime"
rg -Fq 'effective={StateName(colliderActive)}' "$runtime"
rg -Fq 'DiagnosticEvent.Create("DeveloperDiagnostics", "probe_failed")' "$runtime"
rg -Fq 'DeveloperDiagnosticsRuntime.InitializeConsole();' "$plugin"
rg -Fq 'DeveloperDiagnosticsRuntime.Update();' "$plugin"
rg -Fq 'DeveloperDiagnosticsRuntime.Reset();' "$plugin"

if rg -n 'RuntimePrimitiveCatalogCommand|ComfortDiagnosticCommand|CharacterColliderOverlay|bh debug (catalog|comfort|colliders)' "$legacy_client"; then
  printf 'the legacy bh admin command must not dispatch or advertise migrated diagnostics\n' >&2
  exit 1
fi

dotnet run --project "$root/tests/developer-diagnostics-registry/DeveloperDiagnosticsRegistryTests.csproj"

printf 'developer diagnostics registry source and behavior checks passed\n'
