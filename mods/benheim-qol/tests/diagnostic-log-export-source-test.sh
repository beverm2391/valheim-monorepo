#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exporter="$root/src/Infrastructure/DiagnosticLogExporter.cs"
plugin="$root/src/Plugin.cs"

grep -Fq 'InputState.IsKeyDown(KeyCode.F7)' "$exporter"
grep -Fq 'Paths.BepInExRootPath' "$exporter"
grep -Fq 'FileShare.ReadWrite | FileShare.Delete' "$exporter"
grep -Fq 'SpecialFolder.DesktopDirectory' "$exporter"
grep -Fq 'Benheim-log-' "$exporter"
grep -Fq 'DiagnosticLogExporter.Update();' "$plugin"

if rg -n 'InventoryTransaction|BenheimInventoryAudit' "$exporter"; then
  printf 'diagnostic export must not retain transaction-audit coupling\n' >&2
  exit 1
fi

printf 'diagnostic log export source checks passed\n'
