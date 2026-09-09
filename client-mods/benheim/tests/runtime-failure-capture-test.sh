#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source_file="$root/src/Infrastructure/RuntimeFailureCapture.cs"
plugin="$root/src/Plugin.cs"

grep -Fq 'Logger.Listeners.Add(listener);' "$source_file"
grep -Fq 'Logger.Listeners.Remove(current);' "$source_file"
grep -Fq 'DiagnosticEvent.Create("Health", "runtime_failure")' "$source_file"
grep -Fq 'RuntimeFailureCapture.Begin(Paths.BepInExRootPath);' "$plugin"
grep -Fq 'RuntimeFailureCapture.Update();' "$plugin"
grep -Fq 'RuntimeFailureCapture.End();' "$plugin"

dotnet run --project "$root/tests/runtime-failure-capture/RuntimeFailureCaptureTests.csproj"
