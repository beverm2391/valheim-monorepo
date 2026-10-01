#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dotnet run --project "$root/tests/quick-stack-lifecycle/QuickStackLifecycleTests.csproj" -c Release

printf 'Put Away post-lease lifecycle and teardown checks passed\n'
