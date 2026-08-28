#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

dotnet run --project "$root/tests/headshots/HeadshotRulesTests.csproj"

printf 'headshot multiplier and geometry behavior checks passed\n'
