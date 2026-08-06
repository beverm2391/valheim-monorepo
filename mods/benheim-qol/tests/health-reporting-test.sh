#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
plugin="$root/src/Plugin.cs"
health="$root/src/Infrastructure/HealthReporting.cs"
warnings="$root/src/Shortcuts/ShortcutOverlayWarnings.cs"

# The concrete core seam is handled at the patch boundary. Raw Update paths
# remain available for the menu and diagnostic export while gameplay updates
# stop after the failure.
grep -Fq 'harmony.PatchAll();' "$plugin"
grep -Fq 'harmony?.UnpatchSelf();' "$plugin"
grep -Fq 'HealthReporting.DisableCore(ex);' "$plugin"
grep -Fq 'HealthReporting.UpdateCriticalMessage();' "$plugin"
grep -Fq 'DiagnosticLogExporter.Update();' "$plugin"
grep -Fq 'if (!HealthReporting.GameplayActionsEnabled)' "$plugin"
grep -Fq 'loaded_with_gameplay_disabled' "$plugin"
grep -Fq 'LogError($"{CoreFailureMessage} {exceptionText}")' "$health"
grep -Fq 'Diagnostics.Event(' "$health"
grep -Fq '"core_disabled"' "$health"
grep -Fq 'Press Left Shift+B for details.' "$health"

# ZInput construction is a retryable startup state; a ready instance whose
# button map cannot be inspected remains visible in the native Warnings block.
grep -Fq 'if (ZInput.instance == null)' "$warnings"
grep -Fq 'ReportKeybindInspectionFailure' "$warnings"
grep -Fq 'AddHealthWarnings(warnings)' "$warnings"
grep -Fq 'EscapeMarkup' "$warnings"

dotnet run --project "$root/tests/health-reporting/HealthReportingTests.csproj"

printf 'health reporting source and behavioral checks passed\n'
