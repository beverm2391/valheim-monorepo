#!/usr/bin/env bash
set -euo pipefail

# This is the client verification boundary. It checks source and installer
# behavior, exercises the native Put Away summary, and builds the DLL. It does
# not install files, create platform packages, or publish a release.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

for test_script in "$root"/tests/*-test.sh; do
  "$test_script"
done

dotnet run --project "$root/tests/quick-stack-summary/QuickStackSummaryTests.csproj"
"$root/scripts/build.sh"
