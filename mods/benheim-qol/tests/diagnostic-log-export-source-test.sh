#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exporter="$root/src/Infrastructure/DiagnosticLogExporter.cs"
plugin="$root/src/Plugin.cs"

grep -Fq 'Input.GetKeyDown(KeyCode.F7)' "$exporter"
grep -Fq 'TextInput.IsVisible()' "$exporter"
grep -Fq 'Console.IsVisible()' "$exporter"
grep -Fq 'Paths.BepInExRootPath' "$exporter"
grep -Fq 'FileShare.ReadWrite | FileShare.Delete' "$exporter"
grep -Fq 'SpecialFolder.DesktopDirectory' "$exporter"
grep -Fq 'Benheim-log-' "$exporter"
grep -Fq 'InventoryTransactionAudit.GetExistingPaths()' "$exporter"
grep -Fq 'BenheimInventoryAudit.previous.log' "$root/../../shared/benheim-inventory-protocol/InventoryTransactionAudit.cs"
grep -Fq 'DiagnosticLogExporter.Update();' "$plugin"

printf 'diagnostic log export source checks passed\n'
