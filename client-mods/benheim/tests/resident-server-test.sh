#!/usr/bin/env bash
set -euo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
dotnet run --project "$root/server-mods/benheim-server-support/tests/resident-placement/ResidentPlacementTests.csproj"
dotnet run --project "$root/server-mods/benheim-server-support/tests/resident-encounter/ResidentEncounterTests.csproj"
