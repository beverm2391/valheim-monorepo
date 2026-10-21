#!/usr/bin/env bash
set -euo pipefail

# This is the client verification boundary. It checks source and installer
# behavior, exercises the native Put Away summary, and builds the DLL. It does
# not install files, create platform packages, or publish a release.
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
stage="verification startup"
trap 'status=$?; printf "[verify] FAILED during %s (exit %s): %s\n" "$stage" "$status" "$BASH_COMMAND" >&2; exit "$status"' ERR

# Catch failures in these small fixtures before the longer native-source and
# installer checks. Grappling starts with its lifecycle/persistence fixtures.
stage="Grappling fixtures and native source checks"
printf '[verify] %s\n' "$stage"
"$root/tests/grappling-test.sh"

stage="Native mechanic transpiler fixture"
printf '[verify] %s\n' "$stage"
"$root/tests/native-mechanic-transpilers-test.sh"

for test_script in "$root"/tests/*-test.sh; do
  case "${test_script##*/}" in
    grappling-test.sh|native-mechanic-transpilers-test.sh) continue ;;
  esac
  stage="check: ${test_script##*/}"
  printf '[verify] %s\n' "$stage"
  "$test_script"
done

stage="Quick Stack summary fixture"
printf '[verify] %s\n' "$stage"
dotnet run --project "$root/tests/quick-stack-summary/QuickStackSummaryTests.csproj"

stage="Release DLL build"
printf '[verify] %s\n' "$stage"
"$root/scripts/build.sh"

printf '[verify] Verification passed\n'
